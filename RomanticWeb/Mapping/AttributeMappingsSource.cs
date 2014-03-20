using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Anotar.NLog;
using RomanticWeb.Mapping.Attributes;
using RomanticWeb.Mapping.Providers;

namespace RomanticWeb.Mapping
{
    /// <summary>
    /// Mappings repository, which reads mapping attributes from an assembly
    /// </summary>
    public sealed class AttributeMappingsSource:AssemblyMappingsSource
    {
        /// <summary>
        /// Creates a new instance of <see cref="AttributeMappingsSource"/>
        /// </summary>
        public AttributeMappingsSource(Assembly assembly):base(assembly)
        {
            LogTo.Trace("Created attribute mappings repository for assembly {0}", assembly);
        }

        public override IEnumerable<IEntityMappingProvider> GetMappingProviders()
        {
            var builder=new AttributeMappingProviderBuilder();
            return from type in Assembly.GetTypes()
                   let classAtributes = type.GetCustomAttributes<ClassAttribute>()
                   select builder.Visit(type);
        }
    }
}