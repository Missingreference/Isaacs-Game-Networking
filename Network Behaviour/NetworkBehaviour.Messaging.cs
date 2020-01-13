using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using BitStream = MLAPI.Serialization.BitStream;

using Isaac.Network.Messaging;

namespace Isaac.Network
{
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        private RPCReference m_RPCReference;

        public void InvokeServerRPC(RPCDelegate method, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(isServer && !isClient)
            {
                //We are only a server and not a client
                Debug.LogError("Tried to invoke a ServerRPC as only a server and not a host. Only a client can invoke a server RPC.");
                return;
            }

            ulong hash = HashMethod(method.Method);

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    writer.WriteUInt64Packed(hash);

                    stream.CopyFrom(messageStream);

                    if(isHost)
                    {
                        messageStream.Position = 0;
                        //Invoke local
                        InvokeLocalServerRPC(hash, networkManager.clientID, stream);
                    }
                    else
                    {
                        MessageSender.Send(networkManager.serverID, m_NetworkBehaviourManager.serverRPCMessageType, channel, stream);
                    }
                }
            }
        }

        public void InvokeServerRPC(RPCDelegate method, Stream messageStream, string channelName) => InvokeServerRPC(method, messageStream, networkManager.transport.GetChannelByName(channelName));

        public void InvokeClientRPC(RPCDelegate method, ulong clientID, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isServer && isClient)
            {
                //We are only a client
                Debug.LogError("Tried to invoke a ClientRPC as only a client. Only the server can invoke a client RPC.");
                return;
            }

            ulong hash = HashMethod(method.Method);

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    writer.WriteUInt64Packed(hash);

                    stream.CopyFrom(messageStream);

                    if(isHost)
                    {
                        messageStream.Position = 0;
                        //Invoke local
                        InvokeLocalClientRPC(hash, networkManager.clientID, stream);
                    }
                    else
                    {
                        if(!IsNetworkVisibleTo(clientID))
                        {
                            if(IsClientPendingSpawn(clientID))
                            {
                                Debug.LogError("The target client ID '" + clientID + "' is still a pending observer and cannot invoke remote RPCs on it until this Network Behaviour is network visible to that client.", this);
                            }
                            else
                            {
                                Debug.LogError("The target client ID '" + clientID + "' is not an observer of this Network Behaviour.", this);
                            }
                            return;
                        }
                        MessageSender.Send(clientID, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                    }
                }
            }
        }

        public void InvokeClientRPC(RPCDelegate method, ulong clientID, Stream messageStream, string channelName) => InvokeClientRPC(method, clientID, messageStream, networkManager.transport.GetChannelByName(channelName));

        /*
        internal RpcResponse<T> SendServerRPCPerformanceResponse<T>(ulong hash, Stream messageStream, string channel)
        {
            if(!IsClient && IsRunning)
            {
                //We are ONLY a server.
                if(LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Only client and host can invoke ServerRPC");
                return null;
            }

            ulong responseId = ResponseMessageManager.GenerateMessageId();

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(NetworkId);
                    writer.WriteUInt16Packed(NetworkedObject.GetOrderIndex(this));
                    writer.WriteUInt64Packed(hash);

                    if(!IsHost) writer.WriteUInt64Packed(responseId);

                    stream.CopyFrom(messageStream);

                    if(IsHost)
                    {
                        messageStream.Position = 0;
                        object result = InvokeServerRPCLocal(hash, NetworkingManager.Singleton.LocalClientId, messageStream);

                        return new RpcResponse<T>()
                        {
                            Id = responseId,
                            IsDone = true,
                            IsSuccessful = true,
                            Result = result,
                            Type = typeof(T),
                            ClientId = NetworkingManager.Singleton.ServerClientId
                        };
                    }
                    else
                    {
                        RpcResponse<T> response = new RpcResponse<T>()
                        {
                            Id = responseId,
                            IsDone = false,
                            IsSuccessful = false,
                            Type = typeof(T),
                            ClientId = NetworkingManager.Singleton.ServerClientId
                        };

                        ResponseMessageManager.Add(response.Id, response);

                        InternalMessageSender.Send(NetworkingManager.Singleton.ServerClientId, MLAPIConstants.MLAPI_SERVER_RPC_REQUEST, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream, security, null);

                        return response;
                    }
                }
            }
        }*/

        //Does not invoke on all clients but rather invokes on ALL (not pending)observers.
        public void InvokeClientRPCAll(RPCDelegate method, Stream messageStream, string channelName) => InvokeClientRPCAll(method, messageStream, networkManager.transport.GetChannelByName(channelName));

        public void InvokeClientRPCAll(RPCDelegate method, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isServer && isClient)
            {
                //We are only a client
                Debug.LogError("Tried to invoke a ClientRPC as only a client. Only the server can invoke a client RPC.");
                return;
            }

            ulong hash = HashMethod(method.Method);

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    writer.WriteUInt64Packed(hash);

                    stream.CopyFrom(messageStream);

                    using(HashSet<ulong>.Enumerator observers = GetObservers())
                    {
                        while(observers.MoveNext())
                        {
                            if(observers.Current == networkManager.clientID && isHost)
                            {
                                //Invoke local
                                messageStream.Position = 0;
                                InvokeLocalClientRPC(hash, networkManager.clientID, stream);
                            }

                            //Send to remote observer
                            MessageSender.Send(observers.Current, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                        }
                    }
                }
            }
        }

        public void InvokeClientRPCAllExcept(RPCDelegate method, ulong clientIDToIgnore, Stream messageStream, string channelName) => InvokeClientRPCAllExcept(method, clientIDToIgnore, messageStream, networkManager.transport.GetChannelByName(channelName));

        public void InvokeClientRPCAllExcept(RPCDelegate method, ulong clientIDToIgnore, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isServer && isClient)
            {
                //We are only a client
                Debug.LogError("Tried to invoke a ClientRPC as only a client. Only the server can invoke a client RPC.");
                return;
            }

            ulong hash = HashMethod(method.Method);

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    writer.WriteUInt64Packed(hash);

                    stream.CopyFrom(messageStream);

                    using(HashSet<ulong>.Enumerator observers = GetObservers())
                    {
                        while(observers.MoveNext())
                        {
                            if(observers.Current == clientIDToIgnore) continue;
                            if(observers.Current == networkManager.clientID && isHost)
                            {
                                //Invoke local
                                messageStream.Position = 0;
                                InvokeLocalClientRPC(hash, networkManager.clientID, stream);
                            }

                            //Send to remote observer
                            MessageSender.Send(observers.Current, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                        }
                    }
                }
            }
        }

        /*
        internal void SendClientRPCPerformance(ulong hash, List<ulong> clientIDs, Stream messageStream, string channel = null)
        {
            if(!networkManager.isServer && networkManager.isClient)
            {
                //We are NOT a server.
                Debug.LogWarning("Only clients and host can invoke ClientRPC");
                return;
            }

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    writer.WriteUInt64Packed(hash);

                    stream.CopyFrom(messageStream);

                    if(clientIDs == null) //Since this list is null it is assumed it will be sent to all clients. If no clients is wanted then the parameter should be an empty list.
                    {
                        using(List<ulong>.Enumerator clients = networkManager.clients)
                        {
                            while(clients.MoveNext())
                            {
                                if(!IsNetworkVisibleTo(clients.Current))
                                {
                                    if(networkManager.enableLogging)
                                    {
                                        Debug.LogWarning("Silently suppressed ClientRPC on target client '" + clients.Current + "' because it is not visible to the target client.");
                                    }
                                    continue;
                                }
                                if(networkManager.isHost && clients.Current == networkManager.clientID)
                                {
                                    messageStream.Position = 0;
                                    InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                                }
                                else
                                {
                                    MessageSender.Send(clients.Current, networkBehaviourManager.clientRPCMessageType, string.IsNullOrEmpty(channel) ? "NETWORK_DEFAULT" : channel, stream);
                                }
                            }
                        }
                    }
                    else
                    {
                        for(int i = 0; i < clientIDs.Count; i++)
                        {

                            if(networkManager.isHost && clientIDs[i] == networkManager.clientID)
                            {
                                messageStream.Position = 0;
                                InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                            }
                            else
                            {
                                MessageSender.Send(clientIDs[i], networkBehaviourManager.clientRPCMessageType, string.IsNullOrEmpty(channel) ? "NETWORK_DEFAULT" : channel, stream);
                            }
                        }
                    }
                }
            }
        }

        internal void SendClientRPCPerformance(ulong hash, Stream messageStream, ulong clientIDToIgnore, string channel = null)
        {
            if(!networkManager.isServer && networkManager.isClient)
            {
                //We are NOT a server.
                Debug.LogWarning("Only clients and host can invoke ClientRPC");
                return;
            }

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    //writer.WriteUInt16Packed(NetworkedObject.GetOrderIndex(this));
                    writer.WriteUInt64Packed(hash);

                    stream.CopyFrom(messageStream);

                    using(List<ulong>.Enumerator clients = networkManager.clients)
                    {
                        while(clients.MoveNext())
                        {
                            if(clients.Current == clientIDToIgnore)
                                continue;

                            if(!IsNetworkVisibleTo(clients.Current))
                            {
                                if(networkManager.enableLogging)
                                {
                                    Debug.LogWarning("Silently suppressed ClientRPC on target client '" + clients.Current + "' because it is not visible to the target client.");
                                }
                                continue;
                            }

                            if(networkManager.isHost && clients.Current == networkManager.clientID)
                            {
                                messageStream.Position = 0;
                                InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                            }
                            else
                            {
                                MessageSender.Send(clients.Current, networkBehaviourManager.clientRPCMessageType, string.IsNullOrEmpty(channel) ? "NETWORK_DEFAULT" : channel, stream);
                            }
                        }
                    }
                }
            }
        }*/

        /*
        internal RpcResponse<T> SendClientRPCPerformanceResponse<T>(ulong hash, ulong clientId, Stream messageStream, string channel, SecuritySendFlags security)
        {
            if(!IsServer && IsRunning)
            {
                //We are NOT a server.
                if(LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Only clients and host can invoke ClientRPC");
                return null;
            }

            if(!this.NetworkedObject.observers.Contains(clientId))
            {
                if(LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot send ClientRPC to client without visibility to the object");
                return null;
            }

            ulong responseId = ResponseMessageManager.GenerateMessageId();

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(NetworkId);
                    writer.WriteUInt16Packed(NetworkedObject.GetOrderIndex(this));
                    writer.WriteUInt64Packed(hash);

                    if(!(IsHost && clientId == NetworkingManager.Singleton.LocalClientId)) writer.WriteUInt64Packed(responseId);

                    stream.CopyFrom(messageStream);

                    if(IsHost && clientId == NetworkingManager.Singleton.LocalClientId)
                    {
                        messageStream.Position = 0;
                        object result = InvokeClientRPCLocal(hash, NetworkingManager.Singleton.LocalClientId, messageStream);

                        return new RpcResponse<T>()
                        {
                            Id = responseId,
                            IsDone = true,
                            IsSuccessful = true,
                            Result = result,
                            Type = typeof(T),
                            ClientId = clientId
                        };
                    }
                    else
                    {
                        RpcResponse<T> response = new RpcResponse<T>()
                        {
                            Id = responseId,
                            IsDone = false,
                            IsSuccessful = false,
                            Type = typeof(T),
                            ClientId = clientId
                        };

                        ResponseMessageManager.Add(response.Id, response);

                        InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_CLIENT_RPC_REQUEST, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream, security, null);

                        return response;
                    }
                }
            }
        }*/

        public void InvokeLocalServerRPC(ulong hash, ulong senderClientID, Stream stream)
        {
            if(m_RPCReference == null) m_RPCReference = new RPCReference(this);

            if(!m_RPCReference.rpcDefinition.serverMethods.TryGetValue(hash, out ReflectionMethod method))
            {
                Debug.LogError("Tried to invoke a Server RPC but the hash '" + hash + "' does not associate with a Server RPC method.");
                return;
            }

            method.Invoke(m_RPCReference, senderClientID, stream);
        }

        public void InvokeLocalClientRPC(ulong hash, ulong senderClientID, Stream stream)
        {
            if(m_RPCReference == null) m_RPCReference = new RPCReference(this);

            if(!m_RPCReference.rpcDefinition.clientMethods.TryGetValue(hash, out ReflectionMethod method))
            {
                Debug.LogError("Tried to invoke a Client RPC but the hash '" + hash + "' does not associate with a Client RPC method.");
                return;
            }

            method.Invoke(m_RPCReference, senderClientID, stream);
        }
    }
}
