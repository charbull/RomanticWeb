using System;
using System.Collections.Generic;
using System.Linq;
using RomanticWeb.Mapping.Providers;
using RomanticWeb.Mapping.Visitors;

namespace RomanticWeb.Mapping.Fluent
{
    internal class FluentMappingProviderBuilder : IFluentMapsVisitor
    {
        private Type _currentType;

        public IEntityMappingProvider Visit(EntityMap entityMap)
        {
            _currentType = entityMap.Type;
            return new EntityMappingProvider(entityMap.Type, GetClasses(entityMap), GetProperties(entityMap));
        }

        public IClassMappingProvider Visit(ClassMap classMap)
        {
            if (classMap.TermUri != null)
            {
                return new ClassMappingProvider(_currentType, classMap.TermUri);
            }

            return new ClassMappingProvider(_currentType, classMap.NamespacePrefix, classMap.TermName);
        }

        public IPropertyMappingProvider Visit(PropertyMap propertyMap)
        {
            return CreatePropertyMapping(propertyMap);
        }

        public IPropertyMappingProvider Visit(DictionaryMap dictionaryMap, ITermMappingProvider key, ITermMappingProvider value)
        {
            var propertyMapping = CreatePropertyMapping(dictionaryMap);
            return new DictionaryMappingProvider(propertyMapping, key, value);
        }

        public IPropertyMappingProvider Visit(CollectionMap collectionMap)
        {
            var result = new CollectionMappingProvider(CreatePropertyMapping(collectionMap), collectionMap.StorageStrategy);
            if (collectionMap.ElementConverterType != null)
            {
                result.ElementConverterType = collectionMap.ElementConverterType;
            }

            return result;
        }

        public ITermMappingProvider Visit(DictionaryMap.KeyMap keyMap)
        {
            if (keyMap.TermUri != null)
            {
                return new KeyMappingProvider(keyMap.TermUri);
            }

            if (keyMap.NamespacePrefix != null && keyMap.TermName != null)
            {
                return new KeyMappingProvider(keyMap.NamespacePrefix, keyMap.TermName);
            }

            return new KeyMappingProvider();
        }

        public ITermMappingProvider Visit(DictionaryMap.ValueMap valueMap)
        {
            if (valueMap.TermUri != null)
            {
                return new ValueMappingProvider(valueMap.TermUri);
            }

            if (valueMap.NamespacePrefix != null && valueMap.TermName != null)
            {
                return new ValueMappingProvider(valueMap.NamespacePrefix, valueMap.TermName);
            }

            return new ValueMappingProvider();
        }

        private static PropertyMappingProvider CreatePropertyMapping(PropertyMapBase propertyMap)
        {
            PropertyMappingProvider propertyMappingProvider;
            if (propertyMap.TermUri != null)
            {
                propertyMappingProvider = new PropertyMappingProvider(propertyMap.TermUri, propertyMap.PropertyInfo);
            }
            else
            {
                propertyMappingProvider = new PropertyMappingProvider(propertyMap.NamespacePrefix, propertyMap.TermName, propertyMap.PropertyInfo);
            }

            if (propertyMap.ConverterType != null)
            {
                propertyMappingProvider.ConverterType = propertyMap.ConverterType;
            }

            return propertyMappingProvider;
        }

        private IEnumerable<IClassMappingProvider> GetClasses(EntityMap entityMap)
        {
            return entityMap.Classes.Select(c => c.Accept(this)).ToList();
        }

        private IEnumerable<IPropertyMappingProvider> GetProperties(EntityMap entityMap)
        {
            return entityMap.Properties.Select(p => p.Accept(this)).ToList();
        }
    }
}