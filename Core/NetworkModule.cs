using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Elanetic.Network
{
    /// <summary>
    /// The base for any custom addon to the network.
    /// </summary>
    public abstract class NetworkModule
    {
        /// <summary>
        /// The Network Modules that are required to be loaded for this module to work correctly.
        /// </summary>
        public virtual Type[] dependencies { get; } = new Type[] { };
        public NetworkManager networkManager { get; private set; }

        public NetworkModule()
        {
            networkManager = NetworkManager.Get();
            if(networkManager == null)
            {
                Debug.LogError("Network Manager must exist to be able to use Network Modules.");
                return;
            }
        }

        /// <summary>
        /// Called by the Network Manager when it initializes. Here is where variables and arrays should be reset as if it was reinitialized because modules will be reused.
        /// Issues may occur if called by anything but the Network Manager.
        /// </summary>
        public virtual void Init(NetworkModule[] loadedDependencies)
        {

        }

        /// <summary>
        /// Called by the Network manager when the Network Manager is ready, running and the transport is initialized. Useful for when this module needs an initialized transport or events occur after Network Manager is ready and NetworkManager.isRunning is true.
        /// Issues may occur if called by anything but the Network Manager.
        /// </summary>
        public virtual void OnNetworkReady()
        {

        }

        /// <summary>
        /// Called by the Network Manager when it shutsdown. Here is where any events should be unsubscribed and objects deinitialized.
        /// Issues may occur if called by anything but the Network Manager.
        /// </summary>
        public virtual void Shutdown()
        {

        }

        /// <summary>
        /// Called by the Network Manager when a client connects.
        /// Issues may occur if called by anything but the Network Manager. Called right before NetworkManager.onClientConnect event is invoked.
        /// </summary>
        /// <param name="clientID"></param>
        public virtual void OnClientConnect(ulong clientID)
        {

        }

        /// <summary>
        /// Called by the Network Manager when a client disconnects.
        /// Issues may occur if called by anything but the Network Manager. Called right before NetworkManager.onClientDisconnect event is invoked.
        /// </summary>
        /// <param name="clientID"></param>
        public virtual void OnClientDisconnect(ulong clientID)
        {

        }
    }
}