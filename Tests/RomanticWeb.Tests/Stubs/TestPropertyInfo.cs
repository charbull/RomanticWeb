using System;
using System.Globalization;
using System.Reflection;

namespace RomanticWeb.Tests.Stubs
{
    internal class TestPropertyInfo:PropertyInfo
    {
        private readonly Type _type;

        public TestPropertyInfo(Type type)
        {
            _type=type;
        }

        public override string Name
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override Type DeclaringType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override Type ReflectedType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override Type PropertyType
        {
            get
            {
                return _type;
            }
        }

        public override PropertyAttributes Attributes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanRead
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanWrite
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override bool IsDefined(Type attributeType,bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object GetValue(object obj,BindingFlags invokeAttr,Binder binder,object[] index,CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(object obj,object value,BindingFlags invokeAttr,Binder binder,object[] index,CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            throw new NotImplementedException();
        }

        public override MethodInfo GetGetMethod(bool nonPublic)
        {
            throw new NotImplementedException();
        }

        public override MethodInfo GetSetMethod(bool nonPublic)
        {
            throw new NotImplementedException();
        }

        public override ParameterInfo[] GetIndexParameters()
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(Type attributeType,bool inherit)
        {
            throw new NotImplementedException();
        }
    }
}