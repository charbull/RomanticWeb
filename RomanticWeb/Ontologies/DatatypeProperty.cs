﻿using System.Diagnostics;

namespace RomanticWeb.Ontologies
{
    /// <summary>
    /// An Datatype property as defined in the OWL standard
    /// </summary>
    [DebuggerDisplay("Datatype property {Prefix}:{TermName}")]
    public sealed class DatatypeProperty : Property
    {
        /// <summary>
        /// Creates a new instance of <see cref="DatatypeProperty"/>
        /// </summary>
        public DatatypeProperty(string predicateName) : base(predicateName)
        {
        }

        public override string ToString()
        {
            string prefix = Ontology == null ? "_" : Ontology.Prefix;
            return string.Format("Datatype property {0}:{1}", prefix, PropertyName);
        }
    }
}