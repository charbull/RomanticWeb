﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RomanticWeb.ComponentModel;
using RomanticWeb.Configuration;
using RomanticWeb.Converters;
using RomanticWeb.Diagnostics;
using RomanticWeb.Entities;
using RomanticWeb.LightInject;
using RomanticWeb.LinkedData;
using RomanticWeb.Mapping;
using RomanticWeb.Mapping.Conventions;
using RomanticWeb.Mapping.Sources;
using RomanticWeb.Mapping.Visitors;
using RomanticWeb.NamedGraphs;
using RomanticWeb.Ontologies;

namespace RomanticWeb
{
    /// <summary>An entrypoint to RomanticWeb, which encapsulates modularity and creation of <see cref="IEntityContext"/>.</summary>
    public class EntityContextFactory : IEntityContextFactory, IComponentRegistryFacade
    {
        private static readonly object Locker = new object();
        private readonly ServiceContainer _container;
        private IDictionary<Scope, Scope> _trackedScopes;
        private ILogger _log;
        private ILogger _registeredLogger;
        private bool _disposed;
        private bool _threadSafe;

        /// <summary>Initializes a new instance of <see cref="EntityContextFactory"/> class.</summary>
        public EntityContextFactory() : this(new ServiceContainer())
        {
        }

        internal EntityContextFactory(ServiceContainer container)
        {
            _trackedScopes = new Dictionary<Scope, Scope>();
            _registeredLogger = new SimpleLogger();
            _container = container;
            var assemblyNames = from file in Directory.GetFiles(Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath), "RomanticWeb*.dll")
                                let fileName = Path.GetFileName(file)
                                where !fileName.ToLower().Contains("test")
                                select fileName;
            var defaultAssemblies = String.Join("|", assemblyNames.OrderBy(fileName => fileName.Length));
            _container.RegisterAssembly(defaultAssemblies);
            _container.Register<IEntityContextFactory>(f => this);
            _log = new LazyResolvedLogger(() => _registeredLogger);
            _container.RegisterInstance(_log);
            DefaultMappings();
        }

        /// <inheritdoc/>
        public IOntologyProvider Ontologies { get { return new CompoundOntologyProvider(_container.GetAllInstances<IOntologyProvider>()); } }

        /// <inheritdoc/>
        public IMappingsRepository Mappings { get { return _container.GetInstance<IMappingsRepository>(); } }

        /// <inheritdoc/>
        public IEnumerable<IConvention> Conventions { get { return _container.GetAllInstances<IConvention>(); } }

        /// <inheritdoc/>
        public IFallbackNodeConverter FallbackNodeConverter { get { return _container.GetInstance<IFallbackNodeConverter>(); } }

        /// <inheritdoc/>
        public IEnumerable<IMappingModelVisitor> MappingModelVisitors { get { return _container.GetAllInstances<IMappingModelVisitor>(); } }

        /// <inheritdoc />
        public IResultTransformerCatalog TransformerCatalog { get { return _container.GetInstance<IResultTransformerCatalog>(); } }

        /// <inheritdoc />
        public INamedGraphSelector NamedGraphSelector { get { return _container.GetInstance<INamedGraphSelector>(); } }

        /// <inheritdoc />
        public IResourceResolutionStrategy ResourceResolutionStrategy { get { return _container.GetInstance<IResourceResolutionStrategy>(); } }

        internal IEnumerable<Scope> TrackedScopes { get { return _trackedScopes.Values; } }

        internal bool ThreadSafe
        {
            get
            {
                return _threadSafe;
            }

            set
            {
                if (_threadSafe == value)
                {
                    return;
                }

                if (_trackedScopes.Count > 0)
                {
                    throw new InvalidOperationException("Cannot set thread-safety flag. EntityContextFactory already in use.");
                }

                if (_threadSafe = value)
                {
                    _trackedScopes = new ConcurrentDictionary<Scope, Scope>();
                }
                else
                {
                    _trackedScopes = new Dictionary<Scope, Scope>();
                }
            }
        }

        internal bool TrackChanges { get; set; }

        /// <summary>Creates a factory defined in the configuration section.</summary>
        public static EntityContextFactory FromConfiguration(string factoryName)
        {
            var configuration = ConfigurationSectionHandler.Default.Factories[factoryName];
            var ontologies = from element in configuration.Ontologies.Cast<OntologyElement>()
                             select new Ontology(element.Prefix, element.Uri);
            var mappingAssemblies = from element in configuration.MappingAssemblies.Cast<MappingAssemblyElement>()
                                    select Assembly.Load(element.Assembly);

            var entityContextFactory = new EntityContextFactory()
                .WithOntology(new OntologyProviderBase(ontologies))
                .WithMetaGraphUri(configuration.MetaGraphUri)
                .WithMappings(m =>
                {
                    foreach (var mappingAssembly in mappingAssemblies)
                    {
                        m.FromAssembly(mappingAssembly);
                    }
                });
            entityContextFactory.ThreadSafe = configuration.ThreadSafe;
            entityContextFactory.TrackChanges = configuration.TrackChanges;
            if (configuration.BaseUris.Default != null)
            {
                entityContextFactory.WithBaseUri(b => b.Default.Is(configuration.BaseUris.Default));
            }

            return entityContextFactory;
        }

        /// <summary>Creates a new instance of entity context.</summary>
        public IEntityContext CreateContext()
        {
            _log.Debug("Creating entity context");

            lock (Locker)
            {
                var scope = _container.BeginScope();
                _trackedScopes.Add(scope, scope);
                var context = _container.GetInstance<IEntityContext>();
                context.TrackChanges = TrackChanges;
                if (context.Store is IThreadSafeEntityStore)
                {
                    ((IThreadSafeEntityStore)context.Store).ThreadSafe = ThreadSafe;
                }

                context.Disposed += () =>
                    {
                        _trackedScopes.Remove(scope);
                        scope.Dispose();
                    };
                _container.ScopeManagerProvider.GetScopeManager().EndScope(scope);
                return context;
            }
        }

        /// <summary>Includes a given <see cref="IEntitySource" /> in context that will be created.</summary>
        /// <returns>This <see cref="EntityContextFactory" /> </returns>
        public EntityContextFactory WithEntitySource<TSource>() where TSource : IEntitySource
        {
            _container.Register<IEntitySource, TSource>("EntitySource");
            return this;
        }

        /// <summary>Includes a given <see cref="IOntologyProvider" /> in context that will be created.</summary>
        /// <param name="ontologyProvider">Target ontology provider.</param>
        /// <returns>This <see cref="EntityContextFactory" /> </returns>
        public EntityContextFactory WithOntology(IOntologyProvider ontologyProvider)
        {
            // todo: get rid of Guid by refatoring how ontolgies are added
            _container.RegisterInstance(ontologyProvider, Guid.NewGuid().ToString());

            return this;
        }

        /// <summary>Exposes the method to register mapping repositories.</summary>
        /// <param name="buildMappings">Delegate method to be used for building mappings.</param>
        /// <returns>This <see cref="EntityContextFactory" /> </returns>
        public EntityContextFactory WithMappings(Action<MappingBuilder> buildMappings)
        {
            var mappingBuilder = new MappingBuilder(_container.GetAllInstances<IMappingFrom>());
            _container.Invalidate();
            buildMappings.Invoke(mappingBuilder);
            foreach (var source in mappingBuilder.Sources)
            {
                _container.RegisterInstance(source, source.Description);
            }

            return this;
        }

        /// <summary>Exposes a method to define how base <see cref="Uri"/>s are selected for relavitve <see cref="EntityId"/>s.</summary>
        public EntityContextFactory WithBaseUri(Action<BaseUriSelectorBuilder> setupPolicy)
        {
            var builder = new BaseUriSelectorBuilder();
            setupPolicy(builder);
            _container.RegisterInstance(builder.Build());
            return this;
        }

        /// <summary>Exposes a method to define how the default graph name should be obtained.</summary>
        public EntityContextFactory WithNamedGraphSelector(INamedGraphSelector namedGraphSelector)
        {
            _container.RegisterInstance(namedGraphSelector);
            return this;
        }

        /// <summary>Exposes a method to define a logging facility.</summary>
        public EntityContextFactory WithLoggingFacility(ILogger logger)
        {
            _registeredLogger = logger;
            return this;
        }

        /// <summary>Exposes a method to define how the default which resources should be considered external and be obtained.</summary>
        public EntityContextFactory WithResourceResolutionStrategy(IResourceResolutionStrategy resourceResolutionStrategy)
        {
            _container.RegisterInstance(resourceResolutionStrategy);
            return this;
        }

        /// <summary>Sets the meta graph Uri.</summary>
        public EntityContextFactory WithMetaGraphUri(Uri metaGraphUri)
        {
            _container.RegisterInstance(metaGraphUri, "MetaGraphUri");
            return this;
        }

        /// <summary>Registers dependencies from a given <see cref="CompositionRootBase"/> type.</summary>
        public EntityContextFactory WithDependencies<T>() where T : CompositionRootBase, new()
        {
            return WithDependenciesInternal<T>();
        }

        void IComponentRegistryFacade.Register<TService, TComponent>()
        {
            _container.Register<TService, TComponent>();
        }

        void IComponentRegistryFacade.Register<TService>(TService instance)
        {
            _container.RegisterInstance(instance);
        }

        /// <summary>Dispose this entity context factory and all components.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            while (_trackedScopes.Count > 0)
            {
                var scope = _trackedScopes.Values.First();
                scope.Dispose();
            }

            _container.Dispose();
        }

        internal EntityContextFactory WithDependenciesInternal<T>() where T : ICompositionRoot, new()
        {
            _container.RegisterFrom<T>();
            return this;
        }

        private void DefaultMappings()
        {
            var mappingBuilder = new MappingBuilder(new[] { new MappingFromAttributes(_log) });
            mappingBuilder.FromAssemblyOf<ITypedEntity>();
            mappingBuilder.AddMapping(typeof(ITypedEntity).Assembly, new InternalsMappingsSource(typeof(ITypedEntity).Assembly));
            foreach (var source in mappingBuilder.Sources)
            {
                _container.RegisterInstance(source, source.Description);
            }
        }
    }
}