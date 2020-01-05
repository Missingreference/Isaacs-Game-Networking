using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;

using Isaac.Network.Exceptions;

namespace Isaac.Network
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
        /// Changes the owner of the object. Can only be called from the server.
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
                throw new NotServerException("Only the server can call NetworkBehaviour.SetOwner.");
            }

            if(!isNetworkSpawned)
            {
                throw new NetworkException("Cannot change ownership. This Network Behaviour is not spawned.");
            }

            //Owner does not change
            if(targetClientID == ownerID) return;

            if(targetClientID == networkManager.serverID) RemoveOwnership();

            //TODO Send message to clients about ownership change
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes all ownership of an object from any client. Can only be called from the server.
        /// </summary>
        public void RemoveOwnership()
        {
            if(!networkManager.isRunning)
            {
                Debug.LogError("Cannot remove ownership. The network is not running.");
                return;
            }

            if(!isServer)
            {
                throw new NotServerException("Only the server can call NetworkBehaviour.RemoveOwnership.");
            }

            if(!isNetworkSpawned)
            {
                throw new NetworkException("Cannot change ownership. This Network Behaviour is not spawned.");
            }

            //Owner does not change
            if(isOwnedByServer) return;

            OnGainedOwnership();

            //TODO Send message to clients about ownership change
            throw new NotImplementedException();
        }

        protected virtual void OnGainedOwnership() { }

        protected virtual void OnLostOwnership() { }
    }
}