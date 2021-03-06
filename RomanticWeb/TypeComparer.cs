﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RomanticWeb
{
    /// <summary>Compares two types.</summary>
    internal class TypeComparer : IComparer<Type>
    {
        private ISet<AssemblyDependencyEntry> _assemblyDependencyCache;

        private TypeComparer()
        {
            _assemblyDependencyCache = new HashSet<AssemblyDependencyEntry>();
        }

        internal static TypeComparer Default { get { return new TypeComparer(); } }

        /// <inheritdoc />
        int IComparer<Type>.Compare(Type x, Type y)
        {
            if (Object.ReferenceEquals(x, y)) { return 0; }
            if ((Object.ReferenceEquals(x, null)) && (Object.ReferenceEquals(y, null))) { return 0; }
            if (Object.ReferenceEquals(y, null)) { return 1; }
            if (Object.ReferenceEquals(x, null)) { return -1; }
            var yInfo = y.GetTypeInfo();
            var xInfo = x.GetTypeInfo();
            if (yInfo.IsAssignableFrom(x)) { return -1; }
            if (xInfo.IsAssignableFrom(y)) { return 1; }
            if (y.IsAssignableFromSpecificGeneric(x)) { return -1; }
            if (x.IsAssignableFromSpecificGeneric(y)) { return 1; }
            int assemblyXHashCode = xInfo.Assembly.GetHashCode();
            int assemblyYHashCode = yInfo.Assembly.GetHashCode();
            if (assemblyXHashCode != assemblyYHashCode)
            {
                AssemblyDependencyEntry cachedValue = _assemblyDependencyCache
                    .FirstOrDefault(item => (item.AssemblyXHashCode == assemblyXHashCode) && (item.AssemblyYHashCode == assemblyYHashCode));
                if (cachedValue.Equals(AssemblyDependencyEntry.Empty))
                {
                    int? relation = (yInfo.Assembly.GetReferencedAssemblies().Contains(xInfo.Assembly.GetName()) ? (int?)1 :
                        (xInfo.Assembly.GetReferencedAssemblies().Contains(yInfo.Assembly.GetName()) ? (int?)-1 : null));
                    _assemblyDependencyCache.Add(cachedValue = new AssemblyDependencyEntry(assemblyXHashCode, assemblyYHashCode, relation));
                }

                if (cachedValue.Relation.HasValue)
                {
                    return cachedValue.Relation.Value;
                }
            }
            else
            {
                return System.StringComparer.Ordinal.Compare(x.FullName, y.FullName);
            }

            return System.StringComparer.Ordinal.Compare(x.AssemblyQualifiedName, y.AssemblyQualifiedName);
        }

        private struct AssemblyDependencyEntry
        {
            internal static AssemblyDependencyEntry Empty = new AssemblyDependencyEntry(0, 0, null);
            internal int AssemblyXHashCode;
            internal int AssemblyYHashCode;
            internal int? Relation;

            internal AssemblyDependencyEntry(int assemblyXHashCode, int assemblyYHashCode, int? relation)
            {
                AssemblyXHashCode = assemblyXHashCode;
                AssemblyYHashCode = assemblyYHashCode;
                Relation = relation;
            }

            public override int GetHashCode()
            {
                return AssemblyXHashCode ^ AssemblyYHashCode;
            }
        }
    }
}