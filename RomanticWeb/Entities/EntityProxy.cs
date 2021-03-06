﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using RomanticWeb.Collections;
using RomanticWeb.Diagnostics;
using RomanticWeb.Mapping.Model;
using RomanticWeb.Model;
using RomanticWeb.NamedGraphs;

namespace RomanticWeb.Entities
{
    /// <summary>Proxy for exposing mapped entity members.</summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [DebuggerTypeProxy(typeof(DebuggerDisplayProxy))]
    public class EntityProxy : DynamicObject, IEntity, IEntityProxy
    {
        #region Fields
        private readonly IEntityContext _context;
        private readonly IEntityStore _store;
        private readonly Entity _entity;
        private readonly IEntityMapping _entityMapping;
        private readonly IResultTransformerCatalog _resultTransformers;
        private readonly INamedGraphSelector _selector;
        private readonly ILogger _log;
        private readonly IDictionary<int, object> _memberCache = new Dictionary<int, object>();

        private ISourceGraphSelectionOverride _overrideSourceGraph;
        #endregion

        #region Constructors
        /// <summary>Initializes a new instance of the <see cref="EntityProxy" /> class.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="entityMapping">The entity mappings.</param>
        /// <param name="resultTransformers">The result transformers.</param>
        /// <param name="selector">The named graph selector.</param>
        /// <param name="log">Logging facility.</param>
        public EntityProxy(
            Entity entity,
            IEntityMapping entityMapping, 
            IResultTransformerCatalog resultTransformers,
            INamedGraphSelector selector,
            ILogger log)
        {
            _store = (_context = entity.Context).Store;
            _entity = entity;
            _entityMapping = entityMapping;
            _resultTransformers = resultTransformers;
            _selector = selector;
            _log = log;
        }
        #endregion

        #region Properties
        /// <inheritdoc/>
        public EntityId Id { get { return _entity.Id; } }

        /// <summary>Gets the entity mapping.</summary>
        /// <value>The entity mapping.</value>
        public IEntityMapping EntityMapping { get { return _entityMapping; } }

        /// <inheritdoc/>
        public IEntityContext Context { get { return _entity.Context; } }

        /// <summary>Gets the graph selection override.</summary>
        /// <value>The graph selection override.</value>
        public ISourceGraphSelectionOverride GraphSelectionOverride { get { return _overrideSourceGraph; } }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay { get { return _entity.ToString(); } }
        #endregion

        #region Public methods
        /// <inheritdoc />
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var property = _entityMapping.PropertyFor(binder.Name);
            try
            {
                _entity.EnsureIsInitialized();
                var graph = SelectNamedGraph(property);
                int key = _entity.Id.GetHashCode() ^ property.Uri.AbsoluteUri.GetHashCode() ^ (graph != null ? graph.AbsoluteUri.GetHashCode() : 0) ^
                    _context.CurrentCulture.TwoLetterISOLanguageName.GetHashCode();
                if (!_memberCache.TryGetValue(key, out result))
                {
                    var objects = _store.GetObjectsForPredicate(_entity.Id, property.Uri, graph).WhereMatchesContextRequirements(_context);

                    var resultTransformer = _resultTransformers.GetTransformer(property);
                    result = resultTransformer.FromNodes(this, property, Context, objects);

                    if ((result == null) && (property.ReturnType.GetTypeInfo().IsValueType))
                    {
                        result = Activator.CreateInstance(property.ReturnType);
                    }

                    if (result is IEntity)
                    {
                        var entityProxy = ((IEntity)result).UnwrapProxy() as IEntityProxy;

                        if (entityProxy != null)
                        {
                            SetNamedGraphOverride(entityProxy, property);
                        }
                    }

                    _memberCache[key] = result;
                }

                return true;
            }
            catch (Exception e)
            {
                _log.Fatal(e, "An error occured when getting value for property {0}#{1}", _entityMapping.EntityType.FullName, binder.Name);
                throw;
            }
        }

        /// <inheritdoc />
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            CheckIsNotReadonly();

            try
            {
                _entity.EnsureIsInitialized();
                var property = _entityMapping.PropertyFor(binder.Name);
                var graph = SelectNamedGraph(property);
                int key = _entity.Id.GetHashCode() ^ property.Uri.AbsoluteUri.GetHashCode() ^ (graph != null ? graph.AbsoluteUri.GetHashCode() : 0) ^
                    _context.CurrentCulture.TwoLetterISOLanguageName.GetHashCode();
                _memberCache.Remove(key);

                var propertyUri = Node.ForUri(property.Uri);
                var resultTransformer = _resultTransformers.GetTransformer(property);

                Func<IEnumerable<INode>> newValues = () => new INode[0];
                if (value != null)
                {
                    newValues = () => resultTransformer.ToNodes(value, this, property, Context).ToArray();
                }

                _store.ReplacePredicateValues(Id, propertyUri, newValues, graph, _entity.Context.CurrentCulture);
                return true;
            }
            catch
            {
                _log.Fatal("An error occured when setting value for property {0}#{1}", _entityMapping.EntityType.FullName, binder.Name);
                throw;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var proxy = obj as EntityProxy;
            IEntity entity = proxy != null ? proxy._entity : obj as IEntity;
            if (entity == null) { return false; }

            return _entity.Equals(entity);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _entity.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _entity.ToString();
        }

        /// <summary>
        /// Gets the underlying wrapper as another type of entity.
        /// </summary>
        public TInterface AsEntity<TInterface>() where TInterface : class, IEntity
        {
            return _entity.AsEntity<TInterface>();
        }

        /// <summary>
        /// Gets the underlying entity as dynamic.
        /// </summary>
        public dynamic AsDynamic()
        {
            return _entity;
        }

        /// <summary>
        /// Overrides the graph selection.
        /// </summary>
        /// <param name="parametersOverride">The parameters override.</param>
        public void OverrideGraphSelection(ISourceGraphSelectionOverride parametersOverride)
        {
            _overrideSourceGraph = parametersOverride;
        }
        #endregion

        #region Private methods
        private void SetNamedGraphOverride(IEntityProxy proxy, IPropertyMapping property)
        {
            var paremeters = GraphSelectionOverride ?? new OverridingGraphSelector(Id, _entityMapping, property);
            if (proxy != null)
            {
                if ((proxy.Id is BlankId) || (typeof(IRdfListNode<>).IsAssignableFromSpecificGeneric(proxy.EntityMapping.EntityType)))
                {
                    proxy.OverrideGraphSelection(paremeters);
                }
            }
        }

        private Uri SelectNamedGraph(IPropertyMapping property)
        {
            if (_overrideSourceGraph != null)
            {
                return _overrideSourceGraph.SelectGraph(_selector);
            }

            return _selector.SelectGraph(Id, _entityMapping, property);
        }

        private void CheckIsNotReadonly()
        {
            var id = _entity.Id as BlankId;
            if (id == null)
            {
                return;
            }
            
            if (id.RootEntityId == null && id.Graph == null)
            {
                throw new InvalidOperationException(String.Format("Blank node entity <{0}> is read-only.", id));
            }
        }
        #endregion

        private class DebuggerDisplayProxy
        {
            private readonly EntityProxy _proxy;

            public DebuggerDisplayProxy(EntityProxy proxy)
            {
                _proxy = proxy;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public Entity Entity
            {
                get
                {
                    return _proxy._entity;
                }
            }
        }
    }
}