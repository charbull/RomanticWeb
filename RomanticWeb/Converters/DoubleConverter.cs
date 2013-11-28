using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Xml;
using RomanticWeb.Model;
using RomanticWeb.Vocabularies;

namespace RomanticWeb.Converters
{
    /// <summary>
    /// Convert CSD floatign point numbers to doubles
    /// </summary>
    [Export(typeof(ILiteralNodeConverter))]
    public class DoubleConverter:XsdConverterBase
    {
        /// <summary>
        /// Gets Uris of xsd floating point types
        /// </summary>
        protected override IEnumerable<Uri> SupportedTypes
        {
            get
            {
                yield return Xsd.Float;
                yield return Xsd.Double;
            }
        }

        /// <summary>
        /// Converts xsd:float or xsd:double to <see cref="double"/>
        /// </summary>
        public override object Convert(Node objectNode)
        {
            if (objectNode.Literal=="+INF")
            {
                return double.PositiveInfinity;
            }
            
            return XmlConvert.ToDouble(objectNode.Literal);
        }
    }
}