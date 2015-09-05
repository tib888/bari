﻿using System;
using System.Collections.Generic;
using Bari.Core.Model.Parameters;

namespace Bari.Core.Build.Dependencies.Protocol
{
    public class InheritablePropertiesProtocol<TParams, TDef> : IDependencyFingerprintProtocol
        where TDef : ProjectParametersPropertyDefs<TParams>, new() 
        where TParams : InheritableProjectParameters<TParams, TDef>
    {
        /// <summary>
        /// Gets or sets the stored property values
        /// </summary>
        public IDictionary<string, Tuple<object, Type>> Values { get; set; }

        /// <summary>
        /// Creates a new fingerprint from the data stored in the protocol
        /// </summary>
        /// <returns>Returns a fingerprint object which would save the same protocol as this one.</returns>
        public IDependencyFingerprint CreateFingerprint()
        {
            return new InheritablePropertiesFingerprint<TParams, TDef>(this);
        }

        public void Load(IProtocolDeserializerContext context)
        {
            int count = context.ReadInt();
            Values = new Dictionary<string, Tuple<object, Type>>();

            for (int i = 0; i < count; i++)
            {
                string key = context.ReadString();
                object value = context.ReadPrimitive();

                Values.Add(key, Tuple.Create(value, value != null ? value.GetType() : typeof(object)));
            }
        }

        public void Save(IProtocolSerializerContext context)
        {
            context.Write(Values.Count);
            foreach (var pair in Values)
            {
                context.Write(pair.Key);
                context.WritePrimitive(pair.Value.Item1, pair.Value.Item2);
            }
        }
    }
}