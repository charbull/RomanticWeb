﻿using System;
using System.Collections.Generic;
using RomanticWeb.Mapping.Visitors;

namespace RomanticWeb.Mapping.Model
{
    /// <summary>A highest level of entity mapping, used to access property mappings, type mappings, etc.</summary>
    public interface IEntityMapping
    {
        /// <summary>Gets the type of the mapped entity.</summary>
        Type EntityType { get; }

        /// <summary>Gets the RDF type mapping.</summary>
        IEnumerable<IClassMapping> Classes { get; }

        /// <summary>Gets the RDF type mapping.</summary>
        IEnumerable<IPropertyMapping> Properties { get; }

        /// <summary>Gets the RDF type mapping for hidden properties (those overriden with the 'new' operator).</summary>
        IEnumerable<IPropertyMapping> HiddenProperties { get; }

        /// <summary>Gets the property mapping for a property by name.</summary>
        IPropertyMapping PropertyFor(string propertyName);

        /// <summary>Accepts the specified mapping model visitor.</summary>
        /// <param name="mappingModelVisitor">The mapping model visitor.</param>
        void Accept(IMappingModelVisitor mappingModelVisitor);
    }
}