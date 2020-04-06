using System;
using System.IO;
using System.Reflection;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using UnityEngine;
using Elanetic.Network;

namespace MLAPI.Messaging
{
    public class ReflectionMethod
    {
        public readonly MethodInfo method;
        public readonly bool useDelegate;
        public readonly bool serverTarget;
        private readonly bool requireOwnership;
        private readonly int index;
        private readonly Type[] parameterTypes;
        private readonly object[] parameterRefs;

        public static ReflectionMethod Create(MethodInfo method, ParameterInfo[] parameters, int index)
        {
            RPCAttribute[] attributes = (RPCAttribute[])method.GetCustomAttributes(typeof(RPCAttribute), true);

            if(attributes.Length == 0)
                return null;

            if(attributes.Length > 1)
            {
                Debug.LogWarning("Having more than one ServerRPC or ClientRPC attribute per method is not supported.");
            }

            if(method.ReturnType != typeof(void) && !SerializationManager.IsTypeSupported(method.ReturnType))
            {
                Debug.LogWarning("Invalid return type of RPC. Has to be either void or RpcResponse<T> with a serializable type");
            }

            return new ReflectionMethod(method, parameters, attributes[0], index);
        }

        public ReflectionMethod(MethodInfo method, ParameterInfo[] parameters, RPCAttribute attribute, int index)
        {
            this.method = method;
            this.index = index;

            if(attribute is ServerRPCAttribute serverRpcAttribute)
            {
                requireOwnership = serverRpcAttribute.RequireOwnership;
                serverTarget = true;
            }
            else
            {
                requireOwnership = false;
                serverTarget = false;
            }

            if(parameters.Length == 2 && method.ReturnType == typeof(void) && parameters[0].ParameterType == typeof(ulong) && parameters[1].ParameterType == typeof(Stream))
            {
                useDelegate = true;
            }
            else
            {
                useDelegate = false;

                parameterTypes = new Type[parameters.Length];
                parameterRefs = new object[parameters.Length];

                for(int i = 0; i < parameters.Length; i++)
                {
                    parameterTypes[i] = parameters[i].ParameterType;
                }
            }
        }

        public object Invoke(RPCReference rpcReference, ulong senderClientID, Stream stream)
        {
            if(requireOwnership == true && senderClientID != rpcReference.networkBehaviour.ownerID)
            {
                Debug.LogWarning("Only the owner can invoke a Server RPC that is marked to require ownership. (Invoked by client ID '" + senderClientID + "' | Current owner: '" + rpcReference.networkBehaviour.ownerID + "').");

                return null;
            }

            //target.executingRpcSender = senderClientId;

            if(stream.Position == 0)
            {
                if(useDelegate)
                {
                    return InvokeDelegate(rpcReference, senderClientID, stream);
                }
                else
                {
                    return InvokeReflected(rpcReference, stream);
                }
            }
            else
            {
                // Create a new stream so that the stream they get ONLY contains user data and not MLAPI headers
                using(PooledBitStream userStream = PooledBitStream.Get())
                {
                    userStream.CopyUnreadFrom(stream);
                    userStream.Position = 0;

                    if(useDelegate)
                    {
                        return InvokeDelegate(rpcReference, senderClientID, stream);
                    }
                    else
                    {
                        return InvokeReflected(rpcReference, stream);
                    }
                }
            }
        }

        private object InvokeReflected(RPCReference rpcReference, Stream stream)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                for(int i = 0; i < parameterTypes.Length; i++)
                {
                    parameterRefs[i] = reader.ReadObjectPacked(parameterTypes[i]);
                }

                return method.Invoke(rpcReference.networkBehaviour, parameterRefs);
            }
        }

        private object InvokeDelegate(RPCReference rpcReference, ulong senderClientId, Stream stream)
        {
            rpcReference.rpcDelegates[index](senderClientId, stream);

            return null;
        }
    }
}
