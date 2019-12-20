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
        private RPCTypeDefinition rpcDefinition;
        //TODO Configure to not be internal
        internal RPCDelegate[] rpcDelegates;

        public void InvokeServerRPC(RPCDelegate method, Stream messageStream, byte channel=NetworkTransport.DEFAULT_CHANNEL)
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
                        InvokeLocalServerRPC(hash, NetworkManager.Get().clientID, stream);
                    }
                    else
                    {
                        MessageSender.Send(NetworkManager.Get().serverID, m_NetworkBehaviourManager.serverRPCMessageType, channel, stream);
                    }
                }
            }
        }

        public void InvokeServerRPC(RPCDelegate method, Stream messageStream, string channel) => InvokeServerRPC(method, messageStream, NetworkManager.Get().transport.GetChannelByName(channel));

        public void InvokeClientRPC(RPCDelegate method, Stream messageStream, byte channel=NetworkTransport.DEFAULT_CHANNEL)
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
                        InvokeLocalClientRPC(hash, NetworkManager.Get().clientID, stream);
                    }
                    else
                    {
                        MessageSender.Send(NetworkManager.Get().serverID, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                    }
                }
            }
        }

        public void InvokeClientRPC(RPCDelegate method, Stream messageStream, string channel) => InvokeClientRPC(method, messageStream, NetworkManager.Get().transport.GetChannelByName(channel));
        
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

        public void InvokeClientRPCAll(RPCDelegate method, Stream messageStream, byte channel=NetworkTransport.DEFAULT_CHANNEL)
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
                        InvokeLocalClientRPC(hash, NetworkManager.Get().clientID, stream);
                    }
                    else
                    {
                        MessageSender.SendToAllExcept(NetworkManager.Get().serverID, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                    }
                }
            }
        }
        
        public void InvokeClientRPCAllExcept(RPCDelegate method, ulong clientIDToIgnore, Stream messageStream)
        {

        }

        internal void SendClientRPCPerformance(ulong hash, List<ulong> clientIds, Stream messageStream, string channel = null)
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

                    if(clientIds == null)
                    {
                        for(int i = 0; i < networkManager.connectedClients.Count; i++)
                        {
                            /*if(!this.NetworkedObject.observers.Contains(NetworkingManager.Singleton.ConnectedClientsList[i].ClientId))
                            {
                                if(LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogWarning("Silently suppressed ClientRPC because a target in the bulk list was not an observer");
                                continue;
                            }
                            */
                            if(networkManager.isHost && networkManager.connectedClients[i] == networkManager.clientID)
                            {
                                messageStream.Position = 0;
                                InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                            }
                            else
                            {
                                MessageSender.Send(networkManager.connectedClients[i], networkBehaviourManager.clientRPCMessageType, string.IsNullOrEmpty(channel) ? "NETWORK_DEFAULT" : channel, stream);
                            }
                        }
                    }
                    else
                    {
                        for(int i = 0; i < clientIds.Count; i++)
                        {
                            /*if(!this.NetworkedObject.observers.Contains(clientIds[i]))
                            {
                                if(LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot send ClientRPC to client without visibility to the object");
                                continue;
                            }*/

                            if(networkManager.isHost && clientIds[i] == networkManager.clientID)
                            {
                                messageStream.Position = 0;
                                InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                            }
                            else
                            {
                                MessageSender.Send(clientIds[i], networkBehaviourManager.clientRPCMessageType, string.IsNullOrEmpty(channel) ? "NETWORK_DEFAULT" : channel, stream);
                            }
                        }
                    }
                }
            }
        }

        internal void SendClientRPCPerformance(ulong hash, Stream messageStream, ulong clientIdToIgnore, string channel = null)
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


                    for(int i = 0; i < networkManager.connectedClients.Count; i++)
                    {
                        if(networkManager.connectedClients[i] == clientIdToIgnore)
                            continue;

                        /*if(!this.NetworkedObject.observers.Contains(NetworkingManager.Singleton.ConnectedClientsList[i].ClientId))
                        {
                            if(LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogWarning("Silently suppressed ClientRPC because a connected client was not an observer");
                            continue;
                        }*/


                        if(networkManager.isHost && networkManager.connectedClients[i] == networkManager.clientID)
                        {
                            messageStream.Position = 0;
                            InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                        }
                        else
                        {
                            MessageSender.Send(networkManager.connectedClients[i], networkBehaviourManager.clientRPCMessageType, string.IsNullOrEmpty(channel) ? "NETWORK_DEFAULT" : channel, stream);
                        }
                    }
                }
            }
        }

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

        public object InvokeLocalServerRPC(ulong hash, ulong senderClientID, Stream stream)
        {

            if(rpcDefinition.serverMethods.TryGetValue(hash, out ReflectionMethod method))
            {
                return method.Invoke(this, senderClientID, stream);
            }

            return null;
        }

        public object InvokeLocalClientRPC(ulong hash, ulong senderClientID, Stream stream)
        {
            if(rpcDefinition.clientMethods.TryGetValue(hash, out ReflectionMethod method))
            {
                return rpcDefinition.clientMethods[hash].Invoke(this, senderClientID, stream);
            }

            return null;
        }
    }
}
