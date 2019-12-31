using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;

namespace Isaac.Network
{
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Gets the client ID of the owner of this network behaviour.
        /// </summary>
        public ulong ownerClientID
        {
            get
            {
                if(m_OwnerClientID == null)
                    return networkManager != null ? networkManager.serverID : 0;
                return m_OwnerClientID.Value;
            }
            private set
            {
                m_OwnerClientID = value;
            }
        }

        private ulong? m_OwnerClientID = null;

        /// <summary>
        /// Gets whether the behaviour is owned by the local client. Also is true if the network manager is not running in cases where network behaviours are being used offline.
        /// </summary>
        public bool isOwner => (networkManager != null && networkManager.isRunning && ownerClientID == networkManager.clientID) || networkManager == null || !networkManager.isRunning;

        /// <summary>
        /// Gets whether or not the object is owned by the server.
        /// </summary>
        public bool isOwnedByServer => (networkManager != null && ownerClientID == networkManager.serverID) || networkManager == null || !networkManager.isRunning;

        /// <summary>
        /// Changes the owner of the object. Can only be called from server
        /// </summary>
        /// <param name="targetClientID">The new owner clientId</param>
        public void ChangeOwnership(ulong targetClientID)
        {
            //TODO Reimplement ChangeOwnership
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes all ownership of an object from any client. Can only be called from server
        /// </summary>
        public void RemoveOwnership()
        {
            //TODO Reimplement RemoveOwnership
            throw new NotImplementedException();
        }

        protected virtual void OnGainedOwnership()
        {
            throw new NotImplementedException();
        }

        protected virtual void OnLostOwnership()
        {
            throw new NotImplementedException();
        }
    }
}