﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RomanticWeb.Collections;
using RomanticWeb.Dynamic;
using RomanticWeb.Mapping.Model;
using RomanticWeb.Model;

namespace RomanticWeb.Entities.ResultPostprocessing
{
    /// <summary>Transforms the resulting nodes to a <see cref="RdfDictionary{TKey,TValue,TPair,TOwner}"/>.</summary>
    public class DictionaryTransformer : IResultTransformer
    {
        private readonly IDictionaryTypeProvider _typeProvider;

        /// <summary>Initializes a new instance of the <see cref="DictionaryTransformer"/> class.</summary>
        /// <param name="typeProvider">The type provider.</param>
        public DictionaryTransformer(IDictionaryTypeProvider typeProvider)
        {
            _typeProvider = typeProvider;
        }

        /// <summary>Initializes a new instance of the <see cref="DictionaryTransformer"/> class.</summary>
        /// <param name="emitHelper">The code emitting helper.</param>
        public DictionaryTransformer(EmitHelper emitHelper) : this(new DefaultDictionaryTypeProvider(emitHelper))
        {
        }

        /// <summary>Transforms the resulting nodes to a dictionary.</summary>
        /// <param name="parent">The parent entity.</param>
        /// <param name="property">The property.</param>
        /// <param name="context">The context.</param>
        /// <param name="nodes">ignored</param>
        public object FromNodes(IEntityProxy parent, IPropertyMapping property, IEntityContext context, IEnumerable<INode> nodes)
        {
            var constructor = GetDictionaryType(property).GetConstructors().Single(c => c.GetParameters().Count() == 2);
            return constructor.Invoke(new object[] { parent.Id, context });
        }

        /// <summary>Not used.</summary>
        public IEnumerable<INode> ToNodes(object value, IEntityProxy proxy, IPropertyMapping property, IEntityContext context)
        {
            var dictionaryIface = typeof(IDictionary<,>).MakeGenericType(property.ReturnType.GenericTypeArguments).GetTypeInfo();
            var dictionaryType = GetDictionaryType(property);

            if (!dictionaryIface.IsInstanceOfType(value))
            {
                throw new ArgumentException(string.Format("Value must be a of type {0}", dictionaryIface), "value");
            }

            var dictionary = value as IRdfDictionary;
            if (dictionary == null)
            {
                dictionary = (IRdfDictionary)dictionaryType.GetConstructors()
                    .Single(c => c.GetParameters().Length == 3)
                    .Invoke(new[] { proxy.Id, context, value });
            }

            return dictionary.DictionaryEntries.Select(entity => Node.FromEntityId(entity.Id));
        }

        private TypeInfo GetDictionaryType(IPropertyMapping property)
        {
            Type keyType = property.ReturnType.GenericTypeArguments[0];
            Type valueType = property.ReturnType.GenericTypeArguments[1];
            Type pairEntityType = _typeProvider.GetEntryType(property);
            Type ownerType = _typeProvider.GetOwnerType(property);

            return typeof(RdfDictionary<,,,>).MakeGenericType(keyType, valueType, pairEntityType, ownerType).GetTypeInfo();
        }
    }
}