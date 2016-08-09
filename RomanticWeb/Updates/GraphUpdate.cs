using System.Collections.Generic;
using System.Linq;
using RomanticWeb.Entities;
using RomanticWeb.Model;

namespace RomanticWeb.Updates
{
    /// <summary>Represents a change, which updates a named graph.</summary>
    public class GraphUpdate : DatasetChange
    {
        private readonly ISet<IEntityQuad> _removedQuads;
        private readonly ISet<IEntityQuad> _addedQuads;

        internal GraphUpdate(EntityId entityId, EntityId graph, IEntityQuad[] removedQuads, IEntityQuad[] addedQuads) : base(entityId, graph)
        {
            _removedQuads = new HashSet<IEntityQuad>(removedQuads.Except(addedQuads));
            _addedQuads = new HashSet<IEntityQuad>(addedQuads.Except(removedQuads));
        }

        /// <summary>Gets the removed quads.</summary>
        public IEnumerable<IEntityQuad> RemovedQuads { get { return _removedQuads; } }

        /// <summary>Gets the added quads.</summary>
        public IEnumerable<IEntityQuad> AddedQuads { get { return _addedQuads; } }

        /// <inheritdoc />
        public override bool IsEmpty { get { return !_removedQuads.Any() && !_addedQuads.Any(); } }

        /// <summary>Returns a description of the change.</summary>
        public override string ToString()
        {
            return string.Format("Update graph {0}: +{1}/-{2} triples", Graph, _addedQuads.Count(), _removedQuads.Count());
        }

        /// <inheritdoc />
        public override bool CanMergeWith(IDatasetChange other)
        {
            return (other is GraphUpdate || other is GraphReconstruct) && base.CanMergeWith(other);
        }

        /// <inheritdoc />
        public override IDatasetChange MergeWith(IDatasetChange other)
        {
            if (other is GraphReconstruct)
            {
                return other;
            }

            var otherUpdate = (GraphUpdate)other;
            var removalsCombined = RemovedQuads.Union(otherUpdate.RemovedQuads);
            var additionsCombined = AddedQuads.Union(otherUpdate.AddedQuads);

            return new GraphUpdate(Entity, Graph, removalsCombined.ToArray(), additionsCombined.ToArray());
        }
    }
}