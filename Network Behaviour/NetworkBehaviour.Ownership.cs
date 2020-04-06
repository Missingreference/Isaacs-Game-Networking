using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;

using MLAPI.Internal;
using MLAPI.Security;
using MLAPI.Serialization.Pooled;
using BitStream = MLAPI.Serialization.BitStream;

using Elanetic.Network.Exceptions;
using Elanetic.Network.Messaging;

namespace Elanetic.Network
{
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Gets the client ID of the owner of this network behaviour.
        /// </summary>
        public ulong ownerID
        {
            get
            {
                if(networkManager == null || !networkManager.isRunning)
                    throw new NetworkException("Cannot get the owner ID of this Network Behaviour. Network Manager is not running.");
                if(networkBehaviourManager == null)
                    //If you get this exception, make sure that LoadModule<NetworkBehaviourManager> is called at some point on the Network Manager
                    throw new NetworkException("Cannot get the owner ID of this Network Behaviour. The Network Behaviour Manager module is not loaded.");
                return m_OwnerClientID;
            }
        }

        private ulong m_OwnerClientID = 0;

        /// <summary>
        /// Gets whether the behaviour is owned by the local client. Also is true if the network manager is not running in cases where network behaviours are being used offline.
        /// </summary>
        public bool isOwner => (networkManager != null && networkManager.isRunning && ownerID == networkManager.clientID) || networkManager == null || !networkManager.isRunning;

        /// <summary>
        /// Gets whether or not the object is owned by the server.
        /// </summary>
        public bool isOwnedByServer => (networkManager != null && ownerID == networkManager.serverID) || networkManager == null || !networkManager.isRunning;

        /// <summary>
        /// Changes the owner of the object. Can only be called from the server or the owner of this Network Behaviour.
        /// </summary>
        /// <param name="targetClientID">The new owner clientId</param>
        public void SetOwner(ulong targetClientID)
        {
            if(!networkManager.isRunning)
            {
                Debug.LogError("Cannot set ownership. The network is not running.");
                return;
            }

            if(!isServer)
            {
                if(isOwner && targetClientID == networkManager.serverID)
                {
                    RemoveOwnership();
                    return;
                }
                throw new NotServerException("Only the server can call NetworkBehaviour.SetOwner to target anything but the server.");
            }

            if(!isNetworkSpawned)
            {
                throw new NetworkException("Cannot change ownership. This Network Behaviour is not spawned.");
            }

            if(!IsNetworkVisibleTo(targetClientID))
            {
                throw new NetworkException("Cannot change ownership to a client that does not have visibility of this Network Behaviour.");
            }

            //Owner does not change
            if(targetClientID == ownerID) return;

            if(targetClientID == networkManager.serverID)
            {
                RemoveOwnership();
                return;
            }

            if(isOwner)
            {
                m_OwnerClientID = targetClientID;
                OnLostOwnership();
            }
            else //This may seem redundant but we want ownership changes to be set before the OnLostOwnership call
            {
                m_OwnerClientID = targetClientID;
            }

            //Send to all (not pending)observers
            using(PooledBitStream baseStream = PooledBitStream.Get())
            {
                DoOwnershipWrite(baseStream, targetClientID);
                baseStream.PadStream();

                using(BitStream stream = MessagePacker.WrapMessage(networkBehaviourManager.ownerChangeMessageType, 0, baseStream, SecuritySendFlags.None))
                {
                    using(HashSet<ulong>.Enumerator observers = GetObservers())
                    {
                        while(observers.MoveNext())
                        {
                            if(observers.Current == NetworkManager.Get().serverID)
                                continue;

                            NetworkManager.Get().transport.Send(observers.Current, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), networkManager.networkInternalChannel);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all ownership of an object from any client and returns ownership to the server. Can only be called by the server or the owner of this Network Behaviour.
        /// </summary>
        public void RemoveOwnership()
        {
            if(!networkManager.isRunning)
            {
                Debug.LogError("Cannot remove ownership. The network is not running.");
                return;
            }


            if(!isNetworkSpawned)
            {
                throw new NetworkException("Cannot change ownership. This Network Behaviour is not spawned.");
            }

            if(!isServer && !isOwner)
            {
                throw new NotServerException("Only the server can call NetworkBehaviour.RemoveOwnership when they are not the owner of the Network Behaviour.");
            }

            //Owner does not change
            if(isOwnedByServer) return;

            if(isServer)
            {
                m_OwnerClientID = networkManager.serverID;
                OnGainedOwnership();
            }

            using(PooledBitStream baseStream = PooledBitStream.Get())
            {
                DoOwnershipWrite(baseStream, networkManager.serverID);
                if(isServer)
                {
                    baseStream.PadStream();

                    using(BitStream stream = MessagePacker.WrapMessage(networkBehaviourManager.ownerChangeMessageType, 0, baseStream, SecuritySendFlags.None))
                    {
                        using(HashSet<ulong>.Enumerator observers = GetObservers())
                        {
                            while(observers.MoveNext())
                            {
                                if(observers.Current == NetworkManager.Get().serverID)
                                    continue;

                                NetworkManager.Get().transport.Send(observers.Current, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), networkManager.networkInternalChannel);
                            }
                        }
                    }
                }
                else
                {
                    MessageSender.Send(networkManager.serverID, networkBehaviourManager.ownerChangeMessageType, networkManager.networkInternalChannel, baseStream);
                }
            }
        }

        protected virtual void OnGainedOwnership() { }

        protected virtual void OnLostOwnership() { }


        private void DoOwnershipWrite(PooledBitStream stream, ulong newOwner)
        {
            //Do message
            using(PooledBitWriter writer = PooledBitWriter.Get(stream))
            {
                //Write behaviour info and type
                writer.WriteUInt64Packed(networkID);
                writer.WriteUInt64Packed(newOwner);
                writer.WriteBool(ownerCanUnspawn);

                if(networkManager.enableLogging)
                {
                    if(isServer)
                    {
                        Debug.Log("Sending to clients the ownership change.");
                    }
                    else
                    {
                        Debug.Log("Sending to the server the ownership change.");
                    }
                }
            }
        }
    }
}