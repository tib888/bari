﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Bari.Core.Model.Parameters
{
    public class InheritableProjectParameters<TSelf, TPropertyDefs>: IInheritableProjectParameters
        where TPropertyDefs: ProjectParametersPropertyDefs<TSelf>, new() 
        where TSelf : InheritableProjectParameters<TSelf, TPropertyDefs>
    {
        private readonly IDictionary<string, IPropertyValue> propertyValues = new Dictionary<string, IPropertyValue>();
        private readonly TSelf parent;
        private readonly TPropertyDefs defs = new TPropertyDefs();

        public InheritableProjectParameters(TSelf parent = default(TSelf))
        {
            this.parent = parent;

            foreach (var propertyDef in defs.Properties)
            {
                var value = CreateUnspecifiedValue(propertyDef.Type);
                propertyValues.Add(propertyDef.Name, value);
            }
        }

        private static IPropertyValue CreateUnspecifiedValue(Type type)
        {
            return (IPropertyValue)Activator.CreateInstance(typeof (PropertyValue<>).MakeGenericType(type));
        }

        private static IPropertyValue CreateValue<T>(T value)
        {
            return (IPropertyValue) Activator.CreateInstance(typeof (PropertyValue<>).MakeGenericType(typeof (T)), value);
        }

        public bool IsSpecified(string name)
        {
            var value = GetPropertyValue(name);
            return value.IsSpecified || (parent != null && parent.IsSpecified(name));
        }

        protected T? GetAsNullable<T>(string name)
            where T : struct
        {
            if (IsSpecified(name))
                return Get<T>(name);
            else
                return null;
        }

        protected void SetAsNullable<T>(string name, T? value)
            where T : struct
        {
            if (value == null)
                Clear(name);
            else
                Set(name, (T)value);
        }

        public T Get<T>(string name)
        {
            return (T) GetDynamic(name, typeof (T));
        }

        public object GetDynamic(string name, Type t)
        {
            defs.TypeCheck(name, t);

            if (defs.TypeOf(name).IsArray)
            {
                if (defs.MergeWithInherited(name))
                {
                    return GetMergedInheritedValue(name, t);
                } else
                {
                    return GetSimpleInheritedValue(name, t);
                }
            } else
            {
                return GetSimpleInheritedValue(name, t);
            }
        }

        private object GetSimpleInheritedValue(string name, Type t)
        {
            var value = GetPropertyValue(name);
            if (!value.IsSpecified && parent != null)
                return parent.GetDynamic(name, t);

            if (!value.IsSpecified)
                throw new InvalidOperationException("Property value is not specified: " + name);

            return value.Value;
        }

        private object GetMergedInheritedValue(string name, Type arrayType)
        {            
            var elementType = arrayType.GetElementType();
            var value = GetPropertyValue(name);
            if (!value.IsSpecified && parent != null)
                return parent.GetMergedInheritedValue(name, arrayType);

            if (!value.IsSpecified)
                throw new InvalidOperationException("Property value is not specified: " + name);

            if (parent != null)
            {
                if (parent.IsSpecified(name))
                {
                    var parentValue = (Array)parent.GetMergedInheritedValue(name, arrayType);
                    var currentArray = (Array)value.Value;
                    var mergeArrays = typeof(InheritableProjectParametersHelper).GetMethod("MergeArrays");
                    var genericMergeArrays = mergeArrays.MakeGenericMethod(elementType);

                    var res = genericMergeArrays.Invoke(null, new[] { parentValue, currentArray });
                    return res;
                }
            }

            return value.Value;
        }

        public TSelf WithParent(Suite suite, TSelf newParent)
        {
            var copy = defs.CreateDefault(suite, newParent);
            foreach (var pair in propertyValues)
                copy.propertyValues[pair.Key] = pair.Value;

            return copy;
        }

        IInheritableProjectParametersDef IInheritableProjectParameters.Definition
        {
            get { return defs; }
        }

        public IInheritableProjectParameters WithParent(Suite suite, IInheritableProjectParameters newParent)
        {
            return WithParent(suite, (TSelf) newParent);
        }

        protected void Set<T>(string name, T value)
        {
            defs.TypeCheck(name, typeof(T));
            propertyValues[name] = CreateValue(value);
        }

        protected void Clear(string name)
        {
            propertyValues[name] = CreateUnspecifiedValue(defs.TypeOf(name));
        }

        private IPropertyValue GetPropertyValue(string name)
        {
            if (!defs.IsDefined(name))
                throw new ArgumentOutOfRangeException("name", "Property is not defined: " + name);

            return propertyValues[name];            
        }        
    }
}

static class InheritableProjectParametersHelper
{
    public  static TElem[] MergeArrays<TElem>(TElem[] first, TElem[] second)
    {
        return first.Union(second).ToArray();
    }
}