using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using UnityEngine;

using MLAPI.Hashing;

using Isaac.Network;

namespace MLAPI.Messaging
{
    public class RPCTypeDefinition
    {
        private static readonly Dictionary<Type, RPCTypeDefinition> typeLookup = new Dictionary<Type, RPCTypeDefinition>();
        private static readonly Dictionary<ulong, string> hashResults = new Dictionary<ulong, string>();
        private static readonly Dictionary<ulong, Type> typeHashes = new Dictionary<ulong, Type>();

        public static RPCTypeDefinition Get(Type type)
        {
            RPCTypeDefinition info;
            if(!typeLookup.TryGetValue(type, out info))
            {
                info = new RPCTypeDefinition(type);
                typeLookup.Add(type, info);
            }
            return info;
        }

        public static Type GetTypeFromHash(ulong hash)
        {
            if(typeHashes.Count == 0)
            {
                //Get all hashes of NetworkBehaviour
                List<Type> networkBehaviourTypes = AppDomain.CurrentDomain.GetAssemblies()
                       .SelectMany(assembly => assembly.GetTypes())
                       .Where(type => type.IsSubclassOf(typeof(NetworkBehaviour))).ToList();
                for(int i = 0; i < networkBehaviourTypes.Count; i++)
                {
                    Debug.Log("Found NetworkBehaviour type: " + networkBehaviourTypes[i].ToString());
                    typeHashes.Add(networkBehaviourTypes[i].ToString().GetStableHash(NetworkManager.Get().config.rpcHashSize), networkBehaviourTypes[i]);
                }
            }
            Type foundType;
            if(!typeHashes.TryGetValue(hash, out foundType))
            {
                Debug.LogError("No type found associated with hash.");
                return null;
            }
            return foundType;
        }

        public static ulong GetHashFromType(Type type)
        {
            if(typeHashes.Count == 0)
            {
                //Get all hashes of NetworkBehaviour
                List<Type> networkBehaviourTypes = AppDomain.CurrentDomain.GetAssemblies()
                       .SelectMany(assembly => assembly.GetTypes())
                       .Where(nextType => nextType.IsSubclassOf(typeof(NetworkBehaviour))).ToList();
                for(int i = 0; i < networkBehaviourTypes.Count; i++)
                {
                    typeHashes.Add(networkBehaviourTypes[i].ToString().GetStableHash(NetworkManager.Get().config.rpcHashSize), networkBehaviourTypes[i]);
                }
            }
            return typeHashes.FirstOrDefault(x => x.Value == type).Key;
        }

        private static ulong HashMethodNameAndValidate(string name)
        {
            ulong hash = name.GetStableHash(NetworkManager.Get().config.rpcHashSize);

            if(hashResults.ContainsKey(hash))
            {
                string hashResult = hashResults[hash];

                if(hashResult != name)
                {
                    Debug.LogError("Hash collision detected for RPC method. The method \"" + name + "\" collides with the method \"" + hashResult + "\". This can be solved by increasing the amount of bytes to use for hashing in the NetworkConfig or changing the name of one of the conflicting methods.");
                }
            }
            else
            {
                hashResults.Add(hash, name);
            }

            return hash;
        }

        private static List<MethodInfo> GetAllMethods(Type type, Type limitType)
        {
            List<MethodInfo> list = new List<MethodInfo>();

            while(type != null && type != limitType)
            {
                list.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));

                type = type.BaseType;
            }

            return list;
        }

        public readonly Dictionary<ulong, ReflectionMethod> serverMethods = new Dictionary<ulong, ReflectionMethod>();
        public readonly Dictionary<ulong, ReflectionMethod> clientMethods = new Dictionary<ulong, ReflectionMethod>();
        private readonly ReflectionMethod[] delegateMethods;

        private RPCTypeDefinition(Type type)
        {
            List<ReflectionMethod> delegateMethodsList = new List<ReflectionMethod>();
            List<MethodInfo> methods = GetAllMethods(type, typeof(NetworkBehaviour));

            for(int i = 0; i < methods.Count; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                ReflectionMethod rpcMethod = ReflectionMethod.Create(method, parameters, delegateMethodsList.Count);

                if(rpcMethod == null)
                    continue;

                Dictionary<ulong, ReflectionMethod> lookupTarget = rpcMethod.serverTarget ? serverMethods : clientMethods;

                ulong nameHash = HashMethodNameAndValidate(method.Name);

                if(!lookupTarget.ContainsKey(nameHash))
                {
                    lookupTarget.Add(nameHash, rpcMethod);
                }

                if(parameters.Length > 0)
                {
                    ulong signatureHash = HashMethodNameAndValidate(NetworkBehaviour.GetHashableMethodSignature(method));

                    if(!lookupTarget.ContainsKey(signatureHash))
                    {
                        lookupTarget.Add(signatureHash, rpcMethod);
                    }
                }

                if(rpcMethod.useDelegate)
                {
                    delegateMethodsList.Add(rpcMethod);
                }
            }

            delegateMethods = delegateMethodsList.ToArray();
        }

        internal RPCDelegate[] CreateTargetedDelegates(NetworkBehaviour target)
        {
            if(delegateMethods.Length == 0)
                return null;

            RPCDelegate[] rpcDelegates = new RPCDelegate[delegateMethods.Length];

            for(int i = 0; i < delegateMethods.Length; i++)
            {
                rpcDelegates[i] = (RPCDelegate)Delegate.CreateDelegate(typeof(RPCDelegate), target, delegateMethods[i].method.Name);
            }

            return rpcDelegates;
        }
    }
}
