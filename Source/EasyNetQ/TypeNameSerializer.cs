using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
#if NET_STANDARD
using System.Runtime.Loader;
using System.Diagnostics;
#endif

namespace EasyNetQ
{
    public interface ITypeNameSerializer
    {
        string Serialize(Type type);
        Type DeSerialize(string typeName);
    }


    public class TypeNameSerializer : ITypeNameSerializer
    {
        private readonly ConcurrentDictionary<string, Type> deserializedTypes = new ConcurrentDictionary<string, Type>();

        public Type DeSerialize(string typeName)
        {
            Preconditions.CheckNotBlank(typeName, "typeName");

            return deserializedTypes.GetOrAdd(typeName, t =>
            {
                var nameParts = t.Split(':');
                if (nameParts.Length != 2)
                {
                    throw new EasyNetQException("type name {0}, is not a valid EasyNetQ type name. Expected Type:Assembly", t);
                }
                var type = Type.GetType(nameParts[0] + ", " + nameParts[1]);
                if (type == null)
                {
                    type = AlternativeGetType(nameParts[0], nameParts[1]);
                    if (type == null)
                    {
                        throw new EasyNetQException("Cannot find type {0}", t);
                    }
                }
                return type;
            });
        }

        private Type AlternativeGetType(string fullTypeName, string assemblyName)
        {
            return GetLoadedAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == assemblyName)?
                .GetType(fullTypeName, false);
        }

        private IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if NET_STANDARD
            var assemblies = new List<Assembly>();
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                try
                {
                    var assemblyName = AssemblyLoadContext.GetAssemblyName(module.FileName);
                    var assembly = Assembly.Load(assemblyName);
                    assemblies.Add(assembly);
                }
                catch (BadImageFormatException)
                {
                    // ignore native modules
                }
            }
            return assemblies.ToArray();
#endif
#if NETFX
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        private readonly ConcurrentDictionary<Type, string> serializedTypes = new ConcurrentDictionary<Type, string>();

        public string Serialize(Type type)
        {
            Preconditions.CheckNotNull(type, "type");

            return serializedTypes.GetOrAdd(type, t =>
            {

                var typeName = t.FullName + ":" + t.GetTypeInfo().Assembly.GetName().Name;
                if (typeName.Length > 255)
                {
                    throw new EasyNetQException("The serialized name of type '{0}' exceeds the AMQP " +
                                                "maximum short string length of 255 characters.", t.Name);
                }
                return typeName;
            });
        }
    }
}
