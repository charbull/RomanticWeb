﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Anotar.NLog;
using NullGuard;
using RomanticWeb.Entities;
using RomanticWeb.Model;
using RomanticWeb.Updates;

namespace RomanticWeb
{
    internal class EntityStore : IEntityStore
    {
        private readonly IDatasetChangesTracker _changesTracker;
        private readonly ISet<EntityId> _assertedEntities = new HashSet<EntityId>();
        private readonly IDictionary<Node, int> _blankNodeRefCounts = new ConcurrentDictionary<Node, int>();
        private readonly IEntityQuadCollection _entityQuads;
        private readonly IEntityQuadCollection _initialQuads;
        private bool _disposed;

        public EntityStore(IDatasetChangesTracker changesTracker)
        {
            _changesTracker = changesTracker;
            _entityQuads = new EntityQuadCollection2();
            _initialQuads = new EntityQuadCollection2();
        }

        public IEnumerable<EntityQuad> Quads { get { return _entityQuads; } }

        public IDatasetChanges Changes
        {
            get
            {
                return _changesTracker;
            }
        }

        public IEnumerable<Node> GetObjectsForPredicate(EntityId entityId, Uri predicate, [AllowNull] Uri graph)
        {
            var quads = _entityQuads[Node.FromEntityId(entityId), Node.ForUri(predicate)];

            if (graph != null)
            {
                quads = quads.Where(triple => GraphEquals(triple, graph));
            }

            return quads.Select(triple => triple.Object).ToList();
        }

        public IEnumerable<EntityQuad> GetEntityQuads(EntityId entityId)
        {
            return _entityQuads[entityId];
        }

        public IEnumerable<EntityQuad> GetEntityQuads(EntityId entityId, bool includeBlankNodes = true)
        {
            return GetEntityQuads(entityId);
        }

        public void AssertEntity(EntityId entityId, IEnumerable<EntityQuad> entityTriples)
        {
            if (_assertedEntities.Contains(entityId))
            {
                LogTo.Info("Skipping entity {0}. Entity already added to store", entityId);
                return;
            }

            var entityQuads = entityTriples as EntityQuad[] ?? entityTriples.ToArray();
            _entityQuads.Add(entityId, entityQuads);
            _initialQuads.Add(entityId, entityQuads);

            _assertedEntities.Add(entityId);

            foreach (var entityQuad in entityQuads.Where(entityQuad => entityQuad.Object.IsBlank))
            {
                IncrementRefCount(entityQuad.Object);
            }
        }

        public void ReplacePredicateValues(EntityId entityId, Node propertyUri, Func<IEnumerable<Node>> newValues, Uri graphUri)
        {
            var subjectNode = Node.FromEntityId(entityId);
            var removedQuads = RemoveTriples(subjectNode, propertyUri, graphUri).ToArray();
            var newQuads = (from node in newValues()
                            select new EntityQuad(entityId, subjectNode, propertyUri, node).InGraph(graphUri)).ToArray();

            _entityQuads.Add(entityId, newQuads);
            var datasetChange = new GraphUpdate(entityId, graphUri, removedQuads, newQuads);
            _changesTracker.Add(datasetChange);

            foreach (var newQuad in datasetChange.AddedQuads.Where(q => q.Object.IsBlank))
            {
                IncrementRefCount(newQuad.Object);
            }

            var orphanedBlankNodes = from removedQuad in datasetChange.RemovedQuads 
                                     where removedQuad.Object.IsBlank
                                     where DecrementRefCount(removedQuad.Object) == 0
                                     select removedQuad.Object;

            foreach (var orphan in orphanedBlankNodes)
            {
                Delete(orphan.ToEntityId());
            }
        }

        public void Delete(EntityId entityId, DeleteBehaviour deleteBehaviour = DeleteBehaviour.Default)
        {
            var deletes = from entityQuad in _entityQuads[Node.FromEntityId(entityId)].ToList()
                          from removedQuad in RemoveTriple(entityQuad)
                          select removedQuad;

            if (entityId is BlankId)
            {
                deletes = deletes.Union(_entityQuads.RemoveWhereObject(Node.FromEntityId(entityId)));
            }

            var deletesGrouped = (from removedQuad in deletes 
                                  group removedQuad by removedQuad.Graph into g 
                                  select g).ToList();

            if (entityId is BlankId)
            {
                foreach (var removed in deletesGrouped)
                {
                    if (removed.Any(quad => quad.Object.IsBlank || quad.Subject.IsBlank))
                    {
                        var removedQuads = removed;
                        _changesTracker.Add(new GraphReconstruct(entityId, removed.Key.ToEntityId(), Quads.Where(q => q.Graph == removedQuads.Key)));
                    }
                }
            }
            else
            {
                _changesTracker.Add(new EntityDelete(entityId));

                if (deleteBehaviour.HasFlag(DeleteBehaviour.NullifyChildren))
                {
                    _entityQuads.RemoveWhereObject(Node.FromEntityId(entityId));
                    _changesTracker.Add(new RemoveReferences(entityId));
                }
            }
        }

        public void ResetState()
        {
            _initialQuads.Clear();
            foreach (var entityId in _entityQuads)
            {
                _initialQuads.Add(entityId.EntityId, _entityQuads[entityId.EntityId]);
            }

            _changesTracker.Clear();
        }

        public void Rollback()
        {
            _changesTracker.Clear();
            _entityQuads.Clear();
            foreach (var entityId in _initialQuads)
            {
                _entityQuads.Add(entityId.EntityId, _initialQuads[entityId.EntityId]);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _entityQuads.Clear();
            _initialQuads.Clear();

            _disposed = true;
        }

        /// <summary>
        /// Removes triple and blank node's subgraph if present
        /// </summary>
        /// <returns>a value indicating that the was a blank node object value</returns>
        private IEnumerable<EntityQuad> RemoveTriples(Node entityId, Node predicate = null, Uri graphUri = null)
        {
            IEnumerable<EntityQuad> quadsRemoved;

            if (predicate == null)
            {
                quadsRemoved = _entityQuads[entityId];
            }
            else
            {
                quadsRemoved = _entityQuads[entityId, predicate];
            }

            if (graphUri != null)
            {
                quadsRemoved = quadsRemoved.Where(quad => GraphEquals(quad, graphUri));
            }

            return quadsRemoved.ToList().SelectMany(RemoveTriple);
        }

        private IEnumerable<EntityQuad> RemoveTriple(EntityQuad entityTriple)
        {
            _entityQuads.Remove(entityTriple);

            if (entityTriple.Object.IsBlank && !IsReferenced(entityTriple.Object))
            {
                foreach (var removedQuad in RemoveTriples(entityTriple.Object))
                {
                    yield return removedQuad;
                }
            }

            yield return entityTriple;
        }

        // TODO: Make the GraphEquals method a bit less rigid.
        private bool GraphEquals(EntityQuad triple, Uri graph)
        {
            return (triple.Graph.Uri.AbsoluteUri == graph.AbsoluteUri) || ((triple.Subject.IsBlank) && (graph.AbsoluteUri.EndsWith(triple.Graph.Uri.AbsoluteUri)));
        }

        private int DecrementRefCount(Node node)
        {
            AssertIsBlankNode(node);
            return _blankNodeRefCounts[node] -= 1;
        }

        private void IncrementRefCount(Node node)
        {
            AssertIsBlankNode(node);

            if (!_blankNodeRefCounts.ContainsKey(node))
            {
                _blankNodeRefCounts[node] = 0;
            }

            _blankNodeRefCounts[node] += 1;
        }

        private bool IsReferenced(Node node)
        {
            AssertIsBlankNode(node);

            return _blankNodeRefCounts.ContainsKey(node) && _blankNodeRefCounts[node] > 0;
        }

        private void AssertIsBlankNode(Node node)
        {
            if (!node.IsBlank)
            {
                throw new ArgumentOutOfRangeException("node", "Must be blank node");
            }
        }
    }
}