﻿using System;
using System.Collections.Generic;

namespace RomanticWeb.Converters
{
    /// <summary>Contract for implementing a catalog converter.</summary>
    public interface IConverterCatalog
    {
        /// <summary>Gets the URI node converters.</summary>
        IReadOnlyCollection<INodeConverter> UriNodeConverters { get; }

        /// <summary>Gets the literal node converters.</summary>
        IReadOnlyCollection<ILiteralNodeConverter> LiteralNodeConverters { get; }

        /// <summary>Gets the converter.</summary>
        /// <param name="converterType">Type of the converter.</param>
        INodeConverter GetConverter(Type converterType);
    }
}