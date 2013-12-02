﻿using System;
using System.Collections.Generic;
using System.Linq;
using NullGuard;
using Remotion.Linq;
using RomanticWeb.Entities;
using RomanticWeb.Mapping;
using RomanticWeb.Ontologies;

namespace RomanticWeb.Linq
{
    /// <summary>Executes queries against underlying triple store.</summary>
    public class EntityQueryExecutor:IQueryExecutor
    {
        #region Fields
        private readonly IEntityContext _entityContext;
        private readonly IEntitySource _entitySource;
        private readonly IMappingsRepository _mappingsRepository;
        private readonly EntityQueryModelVisitor _modelVisitor;
        #endregion

        #region Constructors
        /// <summary>Creates an instance of the query executor aware of the entities queried.</summary>
        /// <param name="entityContext">Entity factory to be used when creating objects.</param>
        /// <param name="entitySource">Entity source.</param>
        /// <param name="mappingsRepository">Mappings repository to resolve strongly typed properties and types.</param>
        public EntityQueryExecutor(IEntityContext entityContext,IEntitySource entitySource,IMappingsRepository mappingsRepository)
        {
            _entityContext=entityContext;
            _entitySource=entitySource;
            _mappingsRepository=mappingsRepository;
            _modelVisitor=new EntityQueryModelVisitor(_mappingsRepository);
        }
        #endregion

        #region Public methods
        /// <summary>Returns a scalar value beeing a result of a query.</summary>
        /// <typeparam name="T">Type of element to be returned.</typeparam>
        /// <param name="queryModel">Query model to be parsed.</param>
        /// <returns>Single scalar value beeing result of a query.</returns>
        public T ExecuteScalar<T>(QueryModel queryModel)
        {
            RomanticWeb.Linq.Model.Query sparqlQuery=VisitQueryModel(queryModel);
            switch (sparqlQuery.QueryForm)
            {
                case Model.QueryForms.Ask: return (T)Convert.ChangeType(_entitySource.ExecuteAskQuery(sparqlQuery),typeof(T));
                default: return (T)Convert.ChangeType(_entitySource.ExecuteScalarQuery(sparqlQuery),typeof(T));
            }
        }

        /// <summary>Returns a single entity beeing a result of a query.</summary>
        /// <typeparam name="T">Type of element to be returned.</typeparam>
        /// <param name="queryModel">Query model to be parsed.</param>
        /// <param name="returnDefaultWhenEmpty">Tells the executor to return a default value in case of an empty result.</param>
        /// <returns>Single entity beeing result of a query.</returns>
        [return: AllowNull]
        public T ExecuteSingle<T>(QueryModel queryModel,bool returnDefaultWhenEmpty)
        {
            return (returnDefaultWhenEmpty?ExecuteCollection<T>(queryModel).SingleOrDefault():ExecuteCollection<T>(queryModel).Single());
        }

        /// <summary>Returns a resulting collection of a query.</summary>
        /// <typeparam name="T">Type of elements to be returned.</typeparam>
        /// <param name="queryModel">Query model to be parsed.</param>
        /// <returns>Enumeration of resulting entities matching given query.</returns>
        public IEnumerable<T> ExecuteCollection<T>(QueryModel queryModel)
        {
            RomanticWeb.Linq.Model.Query sparqlQuery=VisitQueryModel(queryModel);
            var createMethodInfo=Info.OfMethod("RomanticWeb","RomanticWeb.IEntityContext","Load","EntityId,Boolean").MakeGenericMethod(new[] { typeof(T) });
            ISet<EntityId> ids=new HashSet<EntityId>();
            var groupedTriples=from triple in _entitySource.ExecuteEntityQuery(sparqlQuery)
                               group triple by new { triple.EntityId } into tripleGroup
                               select tripleGroup;

            foreach (var triples in groupedTriples)
            {
                ids.Add(triples.Key.EntityId);
                _entityContext.Store.AssertEntity(triples.Key.EntityId,triples);
            }

            return ids.Select(id => (T)createMethodInfo.Invoke(_entityContext,new object[] { id,false }));
        }
        #endregion

        #region Non-public methods
        private RomanticWeb.Linq.Model.Query VisitQueryModel(QueryModel queryModel)
        {
            _modelVisitor.VisitQueryModel(queryModel);
            return _modelVisitor.Query;
        }
        #endregion
    }
}