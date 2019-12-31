using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

using UnityEngine;

using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using Isaac.Network.Exceptions;

namespace Isaac.Network
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
        /// <param name="clientID">The client to show the object to</param>
        public void NetworkShow(ulong clientID)
        {
            if(!isServer)
            {
                throw new NotServerException("Only the server can change visibility.");
            }

            if(m_Observers.Contains(clientID))
            {
                Debug.LogError("This Network Behaviour is already visible to client '" + clientID + "'.", this);
                return;
            }

            if(!isNetworkSpawned)
            {
                Debug.LogError("This Network Behaviour is not spawned on the network. Make sure this Network Behaviour is spawned using the NetworkBehaviour.SpawnOnNetwork function before changing it's visiblity ", this);
                return;
            }

            // Send spawn call
            m_PendingObservers.Add(clientID);

            throw new NotImplementedException();
            //SpawnManager.SendSpawnCallForObject(clientID, this, payload);
        }

        /// <summary>
        /// Hides a object from a specific client.
        /// Server only.
        /// </summary>
        /// <param name="clientID">The client to hide the object for</param>
        public void NetworkHide(ulong clientID)
        {
            
            if(!NetworkManager.Get().isServer)
            {
                throw new NotServerException("Only the server can change visibility.");
            }

            if(!m_Observers.Contains(clientID))
            {
                Debug.LogError("This Network Behaviour is already not visible to client '" + clientID + "'.", this);
                return;
            }

            if(clientID == NetworkManager.Get().serverID)
            {
                Debug.LogError("Cannot hide an object from the server.");
                return;
            }

            if(!isNetworkSpawned)
            {
                Debug.LogError("This Network Behaviour is not spawned on the network. Make sure this Network Behaviour is spawned using the NetworkBehaviour.SpawnOnNetwork function before changing it's visiblity ", this);
                return;
            }

            // Send destroy call
            m_Observers.Remove(clientID);
            m_PendingObservers.Remove(clientID);

            throw new NotImplementedException();

            /*
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);

                    //InternalMessageSender.SendToAll(MessageType.NETWORK_DESTROY_OBJECT, "NETWORK_INTERNAL", stream);
                }
            }
            */
        }
    }
}