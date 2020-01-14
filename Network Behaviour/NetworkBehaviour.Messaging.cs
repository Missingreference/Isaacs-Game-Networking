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

        #region Server

        public void InvokeServerRPC(RPCDelegate method, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isClient)
            {
                //We are only a server and not a client
                Debug.LogError("Tried to invoke a ServerRPC without being a client. Only a client can invoke a server RPC.", this);
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

        #endregion Server

        #region Client

        public void InvokeClientRPC(RPCDelegate method, ulong clientID, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isServer)
            {
                //We are only a client
                Debug.LogError("Tried to invoke a ClientRPC without being the server. Only the server can invoke a client RPC.", this);
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

                    if(isHost && clientID == networkManager.clientID)
                    {
                        messageStream.Position = 0;
                        //Invoke local
                        InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
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


        //Does not invoke on all clients but rather invokes on ALL (not pending)observers.
        public void InvokeClientRPCAll(RPCDelegate method, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isServer)
            {
                //We are only a client
                Debug.LogError("Tried to invoke a ClientRPC without being the server. Only the server can invoke a client RPC.", this);
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
                                InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                            }

                            //Send to remote observer
                            MessageSender.Send(observers.Current, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                        }
                    }
                }
            }
        }

        public void InvokeClientRPCAll(RPCDelegate method, Stream messageStream, string channelName) => InvokeClientRPCAll(method, messageStream, networkManager.transport.GetChannelByName(channelName));


        public void InvokeClientRPCAllExcept(RPCDelegate method, ulong clientIDToIgnore, Stream messageStream, byte channel = NetworkTransport.DEFAULT_CHANNEL)
        {
            if(!isServer)
            {
                //We are only a client
                Debug.LogError("Tried to invoke a ClientRPC without being the server. Only the server can invoke a client RPC.", this);
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
                                InvokeLocalClientRPC(hash, networkManager.clientID, messageStream);
                            }

                            //Send to remote observer
                            MessageSender.Send(observers.Current, m_NetworkBehaviourManager.clientRPCMessageType, channel, stream);
                        }
                    }
                }
            }
        }

        public void InvokeClientRPCAllExcept(RPCDelegate method, ulong clientIDToIgnore, Stream messageStream, string channelName) => InvokeClientRPCAllExcept(method, clientIDToIgnore, messageStream, networkManager.transport.GetChannelByName(channelName));

        #endregion Client

        #region Local
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

        #endregion Local
    }
}
