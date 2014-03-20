﻿using System.Linq;
using RomanticWeb.Mapping.Providers;

namespace RomanticWeb.Mapping.Model
{
    internal class MappingModelBuilder
    {
        private readonly MappingContext _mappingContext;

        public MappingModelBuilder(MappingContext mappingContext)
        {
            _mappingContext=mappingContext;
        }

        public IEntityMapping BuildMapping(IEntityMappingProvider mapping)
        {
            var classes=mapping.Classes.Select(BuildMapping);
            var properties=mapping.Properties.Select(BuildMapping);

            return new EntityMapping(mapping.EntityType,classes,properties);
        }

        public IPropertyMapping BuildMapping(IPropertyMappingProvider mapping)
        {
            if (mapping is IDictionaryMappingProvider)
            {
                return BuildDictionaryMapping((IDictionaryMappingProvider)mapping);
            }

            if (mapping is ICollectionMappingProvider)
            {
                return BuildCollectionMapping((ICollectionMappingProvider)mapping);
            }

            return BuildPropertyMapping(mapping);
        }

        public IClassMapping BuildMapping(IClassMappingProvider mapping)
        {
            return new ClassMapping(mapping.GetTerm(_mappingContext.OntologyProvider));
        }

        private IPropertyMapping BuildPropertyMapping(IPropertyMappingProvider provider)
        {
            return new PropertyMapping(
                provider.PropertyInfo.PropertyType, 
                provider.PropertyInfo.Name, 
                provider.GetTerm(_mappingContext.OntologyProvider))
                       {
                           Aggregation=provider.Aggregation
                       };
        }

        private IPropertyMapping BuildDictionaryMapping(IDictionaryMappingProvider provider)
        {
            return new DictionaryMapping(
                provider.PropertyInfo.PropertyType, 
                provider.PropertyInfo.Name,
                provider.GetTerm(_mappingContext.OntologyProvider),
                provider.Key.GetTerm(_mappingContext.OntologyProvider),
                provider.Value.GetTerm(_mappingContext.OntologyProvider))
            {
                Aggregation = provider.Aggregation
            };
        }

        private IPropertyMapping BuildCollectionMapping(ICollectionMappingProvider provider)
        {
            return new CollectionMapping(
                provider.PropertyInfo.PropertyType,
                provider.PropertyInfo.Name,
                provider.GetTerm(_mappingContext.OntologyProvider),
                provider.StoreAs)
            {
                Aggregation = provider.Aggregation
            };
        }
    }
}