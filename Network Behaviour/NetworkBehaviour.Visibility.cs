using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

using UnityEngine;

using MLAPI.Serialization.Pooled;
using MLAPI.Messaging;
using MLAPI.Hashing;
using MLAPI.Internal;
using MLAPI.Security;
using BitStream = MLAPI.Serialization.BitStream;

using Elanetic.Network.Exceptions;
using Elanetic.Network.Messaging;


namespace Elanetic.Network
{
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        private readonly HashSet<ulong> m_Observers = new HashSet<ulong>();
        private readonly HashSet<ulong> m_PendingObservers = new HashSet<ulong>();

        /// <summary>
        /// Returns Observers enumerator. Remember to use 'using' keyword or Dispose after use.
        /// Server only.
        /// </summary>
        /// <returns>Observers enumerator</returns>
        public HashSet<ulong>.Enumerator GetObservers()
        {
            if(!isServer)
            {
                throw new NotServerException("Function 'NetworkBehaviour.GetObservers' is not implemented for non-servers.");
            }
            return m_Observers.GetEnumerator();
        }

        /// <summary>
        /// Returns the enumerator for pending observers that still need to be connected on the network.
        /// Server only.
        /// </summary>
        /// <returns></returns>
        public HashSet<ulong>.Enumerator GetPendingObservers()
        {
            if(!isServer)
            {
                throw new NotServerException("Function 'NetworkBehaviour.GetPendingObservers' is not implemented for non-servers.");
            }
            return m_PendingObservers.GetEnumerator();
        }

        /// <summary>
        /// Whether or not this object is visible to a specific client.
        /// Server only.
        /// </summary>
        /// <param name="clientID">The clientId of the client</param>
        /// <returns>True if the client knows about the object</returns>
        public bool IsNetworkVisibleTo(ulong clientID)
        {
            if(!isServer)
            {
                throw new NotServerException("Function 'NetworkBehaviour.IsNetworkVisibleTo' is not implemented for non-servers.");
            }
            return m_Observers.Contains(clientID);
        }

        /// <summary>
        /// Whether or not this object is still pending connection between Network Behaviours on a specific client.
        /// Server only.
        /// </summary>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public bool IsClientPendingSpawn(ulong clientID)
        {
            if(!isServer)
            {
                throw new NotServerException("Function 'NetworkBehaviour.IsClientPendingSpawn' is not implemented for non-servers.");
            }
            return m_PendingObservers.Contains(clientID);
        }

        /// <summary>
        /// Spawns this Network Behaviour on a specific client that does not have visiblity on this client.
        /// Server only.
        /// </summary>
        /// <param name="clientID">The client to show the object to.</param>
        public void NetworkShow(ulong clientID, Stream spawnPayload=null)
        {
            if(m_PendingObservers.Contains(clientID))
            {
                Debug.LogError("This Network Behaviour is already pending visibility to client '" + clientID + "'. Watch for a call to NetworkStart for when it successfully connects.", this);
            }

            if(m_Observers.Contains(clientID))
            {
                Debug.LogError("This Network Behaviour is already visible to client '" + clientID + "'.", this);
                return;
            }

            m_PendingObservers.Add(clientID);

            // Send spawn call
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                DoVisibleShowWrite(stream, spawnPayload);
                MessageSender.Send(clientID, networkBehaviourManager.spawnMessageType, stream);
            }
        }

        /// <summary>
        /// Does not supress errors invloving clients that already have visibility of this Network Behaviour.
        /// Server only.
        /// </summary>
        /// <param name="clientIDs"></param>
        public void NetworkShow(List<ulong> clientIDs, Stream spawnPayload=null)
        {
            //A faster message send bypassing MessageSender completely

            if(clientIDs == null)
            {
                throw new ArgumentNullException(nameof(clientIDs));
            }

            using(PooledBitStream baseStream = PooledBitStream.Get())
            {
                DoVisibleShowWrite(baseStream, spawnPayload);
                baseStream.PadStream();
                if(clientIDs.Count == 0) return; //No one to send to.
                using(BitStream stream = MessagePacker.WrapMessage(networkBehaviourManager.spawnMessageType, 0, baseStream, SecuritySendFlags.None))
                {
                    for(int i = 0; i < clientIDs.Count; i++)
                    {
                        if(clientIDs[i] == NetworkManager.Get().serverID)
                            continue;
                        if(m_PendingObservers.Contains(clientIDs[i]))
                        {
                            Debug.LogError("This Network Behaviour is already pending visibility to client '" + clientIDs[i] + "'. Watch for a call to NetworkStart for when it successfully connects.", this);
                            continue;
                        }
                        if(m_Observers.Contains(clientIDs[i]))
                        {
                            Debug.LogError("This Network Behaviour is already pending visibility to client '" + clientIDs[i] + "'. Watch for a call to NetworkStart for when it successfully connects.", this);
                            continue;
                        }
                        m_PendingObservers.Add(clientIDs[i]);

                        NetworkManager.Get().transport.Send(clientIDs[i], new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), networkManager.networkInternalChannel);
                    }
                }
            }
        }

        /// <summary>
        /// Suppresses errors involving clients that already have visibility of this Network Behaviour.
        /// Server only.
        /// </summary>
        public void NetworkShowAll(Stream spawnPayload=null)
        {
            //A faster message send bypassing MessageSender completely

            using(PooledBitStream baseStream = PooledBitStream.Get())
            {
                DoVisibleShowWrite(baseStream, spawnPayload);
                baseStream.PadStream();

                using(BitStream stream = MessagePacker.WrapMessage(networkBehaviourManager.spawnMessageType, 0, baseStream, SecuritySendFlags.None))
                {
                    using(List<ulong>.Enumerator clients = networkManager.clients)
                    {
                        while(clients.MoveNext())
                        {
                            if(clients.Current == NetworkManager.Get().serverID)
                                continue;
                            if(m_PendingObservers.Contains(clients.Current) || m_Observers.Contains(clients.Current))
                                continue;

                            m_PendingObservers.Add(clients.Current);

                            NetworkManager.Get().transport.Send(clients.Current, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), networkManager.networkInternalChannel);
                        }
                    }
                }
            }
        }

        private void DoVisibleShowWrite(PooledBitStream stream, Stream spawnPayload)
        {
            if(!isServer)
            {
                throw new NotServerException("Only the server can change visibility of a Network Behaviour.");
            }

            if(!isNetworkSpawned)
            {
                throw new NetworkException("This Network Behaviour is not spawned on the network. Make sure this Network Behaviour is spawned using the NetworkBehaviour.SpawnOnNetwork function before changing it's visiblity.");
            }

            //Do message
            using(PooledBitWriter writer = PooledBitWriter.Get(stream))
            {
                //Write behaviour info and type
                writer.WriteUInt64Packed(networkID);
                writer.WriteUInt64Packed(ownerID);
                writer.WriteUInt64Packed(RPCTypeDefinition.GetHashFromType(GetType()));
                if(string.IsNullOrWhiteSpace(uniqueID))
                {
                    writer.WriteBool(false);
                }
                else
                {
                    writer.WriteBool(true);
                    writer.WriteUInt64Packed(m_UniqueHash);
                }
                writer.WriteBool(ownerCanUnspawn);
                writer.WriteBool(destroyOnUnspawn);

                //Write payload
                writer.WriteBool(spawnPayload != null);

                if(spawnPayload != null)
                {
                    spawnPayload.Position = 0;
                    writer.WriteInt32Packed((int)spawnPayload.Length);
                    stream.CopyFrom(spawnPayload);
                }

                if(networkManager.enableLogging)
                    Debug.Log("Sending to clients the new behaviour " + GetType());
            }
        }

        /// <summary>
        /// Hides an object from a specific client.
        /// Server only.
        /// </summary>
        /// <param name="clientID">The client to hide the object from.</param>
        public void NetworkHide(ulong clientID)
        {

            if(!m_PendingObservers.Contains(clientID) && !m_Observers.Contains(clientID))
            {
                Debug.LogError("This Network Behaviour is already not visible to client '" + clientID + "'.", this);
                return;
            }

            if(clientID == NetworkManager.Get().serverID)
            {
                Debug.LogError("Cannot hide an object from the server.");
                return;
            }

            // Send destroy call
            m_Observers.Remove(clientID);
            m_PendingObservers.Remove(clientID);

            //Check if the client is even still connected
            if(!networkManager.IsClient(clientID)) return;

            // Send unspawn call
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                DoVisibleHideWrite(stream);
                MessageSender.Send(clientID, networkBehaviourManager.unspawnMessageType, stream);
            }
        }

        /// <summary>
        /// Hides an object from a specific clients.
        /// Server only.
        /// </summary>
        /// <param name="clientIDs">The clients to hide the object from.</param>
        public void NetworkHide(List<ulong> clientIDs)
        {
            //A faster message send bypassing MessageSender completely

            if(clientIDs == null)
            {
                throw new ArgumentNullException(nameof(clientIDs));
            }

            using(PooledBitStream baseStream = PooledBitStream.Get())
            {
                DoVisibleHideWrite(baseStream);
                baseStream.PadStream();
                if(clientIDs.Count == 0) return; //No one to send to.
                using(BitStream stream = MessagePacker.WrapMessage(networkBehaviourManager.unspawnMessageType, 0, baseStream, SecuritySendFlags.None))
                {
                    for(int i = 0; i < clientIDs.Count; i++)
                    {
                        if(clientIDs[i] == networkManager.serverID)
                            continue;
                        if(!m_PendingObservers.Remove(clientIDs[i]) && !m_Observers.Remove(clientIDs[i]))
                        {
                            Debug.LogError("This Network Behaviour is already not visible to client '" + clientIDs[i] + "'.", this);
                            continue;
                        }

                        networkManager.transport.Send(clientIDs[i], new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), networkManager.networkInternalChannel);
                    }
                }
            }
        }

        /// <summary>
        /// Hides an object from all clients.
        /// Server only.
        /// </summary>
        public void NetworkHideAll()
        {
            //A faster message send bypassing MessageSender completely

            using(PooledBitStream baseStream = PooledBitStream.Get())
            {
                DoVisibleHideWrite(baseStream);
                baseStream.PadStream();

                using(BitStream stream = MessagePacker.WrapMessage(networkBehaviourManager.unspawnMessageType, 0, baseStream, SecuritySendFlags.None))
                {
                    using(List<ulong>.Enumerator clients = networkManager.clients)
                    {
                        while(clients.MoveNext())
                        {
                            if(clients.Current == networkManager.serverID)
                                continue;
                            if(!m_PendingObservers.Remove(clients.Current) && !m_Observers.Remove(clients.Current))
                                continue;

                            networkManager.transport.Send(clients.Current, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), networkManager.networkInternalChannel);
                        }
                    }
                }
            }
        }

        private void DoVisibleHideWrite(PooledBitStream stream)
        {
            if(!isServer)
            {
                throw new NotServerException("Only the server can change visibility of a Network Behaviour.");
            }

            if(!isNetworkSpawned)
            {
                throw new NetworkException("This Network Behaviour is not spawned on the network. Make sure this Network Behaviour is spawned using the NetworkBehaviour.SpawnOnNetwork function before changing it's visiblity.");
            }

            //Do message
            using(PooledBitWriter writer = PooledBitWriter.Get(stream))
            {
                writer.WriteUInt64Packed(networkID);
                writer.WriteBool(destroyOnUnspawn);

                if(networkManager.enableLogging)
                    Debug.Log("Sending to clients an unspawn message for '" + GetType() + "'.");
            }
        }
    }
}