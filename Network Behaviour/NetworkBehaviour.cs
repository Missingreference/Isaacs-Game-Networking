using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Text;

using UnityEngine;

using MLAPI.Hashing;
using MLAPI.Messaging;
using MLAPI.NetworkedVar;
using MLAPI.Reflection;
using MLAPI.Security;
using MLAPI.Spawning;
using BitStream = MLAPI.Serialization.BitStream;

using Isaac.Network.Messaging;
using Isaac.Network.Spawning;
using Isaac.Network.Exceptions;

namespace Isaac.Network
{
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Static

        private ulong HashMethod(MethodInfo method)
        {
            if(methodInfoHashTable.ContainsKey(method))
            {
                return methodInfoHashTable[method];
            }

            ulong hash = GetHashableMethodSignature(method).GetStableHash(NetworkManager.Get().config.rpcHashSize); //HashMethodName(GetHashableMethodSignature(method));
            methodInfoHashTable.Add(method, hash);

            return hash;
        }

        private static readonly StringBuilder methodInfoStringBuilder = new StringBuilder();
        private static readonly Dictionary<MethodInfo, ulong> methodInfoHashTable = new Dictionary<MethodInfo, ulong>();
        internal static string GetHashableMethodSignature(MethodInfo method)
        {
            methodInfoStringBuilder.Length = 0;
            methodInfoStringBuilder.Append(method.Name);

            ParameterInfo[] parameters = method.GetParameters();

            for(int i = 0; i < parameters.Length; i++)
            {
                methodInfoStringBuilder.Append(parameters[i].ParameterType.Name);
            }

            return methodInfoStringBuilder.ToString();
        }

        #endregion

        /// <summary>
        /// Is true if this Network Behaviour is spawned whether it is pending connection or connected. False means it is not connected and not trying to connect across the network.
        /// </summary>
        public bool isNetworkSpawned => m_IsNetworkSpawned;

        /// <summary>
        /// Is true if this Network Behaviour is not pending and is connected across the network to at least one other Network Behaviour across the network.
        /// </summary>
        public bool isNetworkReady => m_IsNetworkReady;

        /// <summary>
        /// Gets the ID of this object that is unique and synced across the network.
        /// </summary>
        public ulong networkID
        {
            get
            {
                if(!isNetworkReady)
                {
                    throw new NetworkException("Cannot get network ID. This Network Behaviour is not network ready.");
                }
                return m_NetworkID;
            }
        }

        /// <summary>
        /// Unique identifier for syncing across the network. Only used for trying to sync. Use Network ID for identifying a behaviour across the network. Setting this on the server won't send the clients it's details and won't assume that an exisitng object is trying to sync on the client.
        /// </summary>
        public string uniqueID
        {
            get
            {
#if UNITY_EDITOR
                if(isNetworkSpawned && m_UniqueID != m_KnownUniqueID)
                {
                    m_UniqueID = m_KnownUniqueID;
                    Debug.LogWarning("Inspector changes to unique ID have been reset because the unique ID cannot be altered while this Network Behaviour is spawned on the network", this);
                }
#endif
                return m_UniqueID;
            }
            set
            {
                if(isNetworkSpawned)
                {
                    Debug.LogWarning("Cannot change the unique ID while this Network Behaviour is spawned.", this);
                    return;
                }
                m_UniqueID = value;
            }
        }

        [Header("Network")]
        [Tooltip("Unique identifier for syncing across the network. Leaving this blank on the server will have it create an instance across the network.")]
        [SerializeField]
        private string m_UniqueID = string.Empty;

        /// <summary>
        /// If true, SpawnOnNetwork will be called when a scene loads.
        /// </summary>
        [Tooltip("Set this to true if you want this Network Behaviour to spawn on the network once ANY scene loads.")]
        [SerializeField]
        public bool spawnOnSceneLoad = true;

        /// <summary>
        /// If true, SpawnOnNetwork will be called when the Network Manager initializes.
        /// </summary>
        [Tooltip("Set this to true if you want this Network Behaviour to spawn on the network once the Network Manager initializes.")]
        [SerializeField]
        public bool spawnOnNetworkInit = true;

        public bool destroyOnUnspawn
        {
            get
            {
                return m_DestroyOnUnspawn;
            }
            set
            {
                if(!isOwner)
                {
                    throw new NotServerException("Only the server or owner can set destroyOnUnspawn property.");
                }
                m_DestroyOnUnspawn = value;
            }
        }

        /// <summary>
        /// Set whether the owner(client) of this Network Behaviour can unspawn on network. Server can unspawn regardless of the setting of this property. Only the server can set this settings. It is recommended that this setting is set before spawning on the network.
        /// Changing this value will send a message to all clients.
        /// </summary>
        public bool ownerCanUnspawn
        {
            get
            {
                return m_OwnerCanUnspawn;
            }
            set
            {
                if(networkManager.isRunning && !isServer)
                {
                    throw new NotServerException("Only the server can set the ownerCanUnspawn property.");
                }
                m_OwnerCanUnspawn = value;
            }
        }

        /// <summary>
        /// Reference to the Network Manager.
        /// </summary>
        protected NetworkManager networkManager 
		{ 
			get
			{
				if(m_NetworkManager == null)
				{
                    m_NetworkBehaviourManager = null;
                    m_NetworkManager = NetworkManager.Get();
				}
				return m_NetworkManager;
			}
		}

        //Short hands
        protected bool isServer => networkManager && networkManager.isServer;
        protected bool isClient => networkManager && networkManager.isClient;
        protected bool isHost => networkManager && networkManager.isHost;

        protected NetworkBehaviourManager networkBehaviourManager
        {
            get
            {
                if(m_NetworkBehaviourManager == null)
                {
                    m_NetworkBehaviourManager = networkManager.GetModule<NetworkBehaviourManager>();
                }
                return m_NetworkBehaviourManager;
            }
        }

        private NetworkManager m_NetworkManager = null;
        private NetworkBehaviourManager m_NetworkBehaviourManager = null;
        private ulong m_NetworkID = 0;
        private ulong m_UniqueHash = 0;
        private bool m_IsNetworkSpawned = false;
        private bool m_IsNetworkReady = false;
        private bool m_DestroyOnUnspawn = true;
        private bool m_OwnerCanUnspawn = false;


#if UNITY_EDITOR
        //This is to make sure that unique ID is not altered in the inspector when isNetworkSpawned is true.
        private string m_KnownUniqueID = string.Empty;
#endif

        /// <summary>
        /// When this is called it will begin its connection to its matching Network Behaviour across the network. Until this function is called it will act as a normal Monobehaviour.
        /// Default owner is the server. Default visibility is all clients.
        /// </summary>
        public void SpawnOnNetwork(Stream stream = null)
        {
            if(!IsValidSpawn()) return;

            DoSpawnOnNetwork(networkManager.serverID, null, null);
        }

        /// <summary>
        /// When this is called it will begin its connection to its matching Network Behaviour across the network. Until this function is called it will act as a normal Monobehaviour.
        /// This variant of the function should only be called by the server since only the server can spawn a Network Behaviour with ownership.
        /// </summary>
        /// <param name="ownerID">The client ID of the </param>
        public void SpawnOnNetwork(ulong ownerID, Stream stream = null)
        {
            if(!IsValidSpawn()) return;

            DoSpawnOnNetwork(ownerID, null, null);
        }

        /// <summary>
        /// When this is called it will begin its connection to its matching Network Behaviour across the network. Until this function is called it will act as a normal Monobehaviour.
        /// This variant of the function should only be called by the server since only the server can spawn a Network Behaviour with ownership and alter visibility.
        /// </summary>
        /// <param name="ownerID">The client ID of the target client that will be the owner of this Network Behaviour.</param>
        /// <param name="observers">The list of clients that will have visibility of this Network Behaviour.</param>
        public void SpawnOnNetwork(ulong ownerID, List<ulong> observers, Stream stream = null)
        {
            if(!IsValidSpawn()) return;

            DoSpawnOnNetwork(ownerID, observers, null);
        }

        private bool IsValidSpawn()
        {
            if(networkManager == null || !networkManager.isRunning)
            {
                Debug.LogError("Unable to spawn. The Network Manager is not running.");
                return false;
            }

            if(networkBehaviourManager == null)
            {
                //If this occurs make sure that LoadModule<NetworkBehaviourManager>() is called before the initialization of the Network Manager.
                Debug.LogError("Unable to spawn. The Network Manager does not have the Network Behaviour Manager module loaded.");
                return false;
            }

            if(m_IsNetworkSpawned)
            {
                Debug.LogError("This Network Behaviour is already spawned on the network.", this);
                return false;
            }

            if(this == null)
            {
                Debug.LogError("Unable to spawn. This Network Behaviour has been destroyed.", this);
                return false;
            }

            return true;
        }

        private void DoSpawnOnNetwork(ulong ownerID, List<ulong> observers, Stream stream)
        {
#if UNITY_EDITOR
            m_KnownUniqueID = m_UniqueID;
#endif

            if(stream != null)
                Debug.LogWarning("Stream parameter is not yet implemented.");

            m_UniqueHash = m_UniqueID.GetStableHash(networkManager.config.rpcHashSize);

            if(isServer) //Server
            {
                try
                {
                    m_IsNetworkSpawned = true;
                    m_NetworkBehaviourManager.SpawnOnNetworkServer(this, OnBehaviourConnected, OnBehaviourDisconnected, OnServerRPCReceived, ownerID, observers);
                }
                catch(Exception ex)
                {
                    m_IsNetworkSpawned = false;
                    //Look down the call stack for the source of the exception
                    throw;
                }
            }
            else //Client
            {
                if(ownerID != networkManager.serverID)
                {
                    throw new NotServerException("Only the server can set the owner of this Network Behaviour when spawning on the network.");
                }

                if(observers != null)
                {
                    throw new NotServerException("Only the server can change the visibility of this Network Behaviour when spawning on the network.");
                }

                try
                {
                    m_IsNetworkSpawned = true;
                    m_NetworkBehaviourManager.SpawnOnNetworkClient(this, OnBehaviourConnected, OnBehaviourDisconnected, OnClientRPCReceived);
                }
                catch(Exception ex)
                {
                    m_IsNetworkSpawned = false;
                    throw;
                }
            }
        }

        /// <summary>
        /// When this is called it will disconnect this Network Behaviour from the existing Network Behaviours across the network. Can only be called by the server or the owner of this Network Behaviour.
        /// </summary>
        public void UnspawnOnNetwork()
        {
            if(!m_IsNetworkSpawned) //This should only be enabled if the Network Manager is running so no point in checking for a valid Network Manager.
            {
                Debug.LogError("Cannot unspawn this Network Behaviour. It is not spawned on the network.", this);
                return;
            }
            if(!isOwner && !networkManager.isServer && networkManager.isClient)
            {
                throw new NotServerException("Only the owner of this Network Behaviour or the server can unspawn this Network Behaviour.");
            }

            if(networkManager.isRunning)
            {
                m_NetworkBehaviourManager.UnspawnOnNetwork(this);
            }
            
            if(!isNetworkReady && destroyOnUnspawn && this != null)
            {
                Destroy(gameObject);
            }

            m_IsNetworkSpawned = false;
            m_IsNetworkReady = false;
        }

        /// <summary>
        /// Called when this Network Behaviour successfully connects to a similar Network Behaviour across the network.
        /// </summary>
        /// <param name="clientID">
        /// The client/server that successfully connected behaviours across the network. Clients will only received the server's ID as the value.
        /// </param>
        protected virtual void OnNetworkReady(ulong clientID) { }

        /// <summary>
        /// Called when this Network Behaviour disconnects.
        /// </summary>
        protected virtual void OnNetworkShutdown() { }

        //Server
        private void OnBehaviourConnected(ulong newNetworkID, ulong clientID, List<ulong> observers)
        {
            m_NetworkID = newNetworkID;

            //Since were the server, send all the specified observers(clients) the spawning Network Behaviour
            if(clientID == networkManager.serverID)
            {
                //Set early since this will be called 
                m_IsNetworkSpawned = true;
                m_IsNetworkReady = true;

                //Add server as observer
                m_Observers.Add(clientID);

                if(observers == null)
                    NetworkShowAll();
                else
                    NetworkShow(observers);
            }
            else
            {
                if(m_PendingObservers.Remove(clientID))
                {
                    m_Observers.Add(clientID);
                }
                else
                {
                    if(m_Observers.Contains(clientID))
                    {
                        Debug.LogError("Client '" + clientID + "' sent a successful visibility message when it was already an observer.");
                        return;
                    }
                    else
                    {
                        Debug.LogError("Client '" + clientID + "' sent a successful visibility message when it is not pending visibility.");
                        return;
                    }
                }
            }

            //Call Network start function
            OnNetworkReady(clientID);
        }

        //Client
        private void OnBehaviourConnected(ulong newNetworkID, bool ownerCanUnspawnSetting, bool destroyOnUnspawnSetting)
        {
            m_NetworkID = newNetworkID;

            //Set early in case the Network Behaviour Manager remote
            m_IsNetworkSpawned = true;
            m_IsNetworkReady = true;

            //Update Network Behaviour properties
            m_OwnerCanUnspawn = ownerCanUnspawnSetting;
            m_DestroyOnUnspawn = destroyOnUnspawnSetting;

            //Call Network start function
            OnNetworkReady(networkManager.serverID);
        }

        private void OnBehaviourUpdate(bool ownerCanUnspawnSetting, bool destroyOnUnspawnSetting, ulong ownerID, bool isLostObserver, ulong observer)
        {

        }


        private void OnBehaviourDisconnected(bool ownerCanUnspawnSetting, bool destroyOnUnspawnSetting)
        {
            Debug.Log("Behaviour disconnected");
            if(networkManager.isRunning)
                m_IsNetworkSpawned = false;
            m_IsNetworkReady = false;
            m_OwnerCanUnspawn = ownerCanUnspawnSetting;
            m_DestroyOnUnspawn = destroyOnUnspawnSetting;
            m_NetworkID = 0;

            OnNetworkShutdown();

            if(destroyOnUnspawn && this != null)
            {
                Destroy(gameObject);
            }
        }

        private void OnClientRPCReceived(ulong hash, ulong senderClientID, Stream stream)
        {
            InvokeLocalClientRPC(hash, senderClientID, stream);
        }

        private void OnServerRPCReceived(ulong hash, ulong senderClientID, Stream stream)
        {
            InvokeLocalServerRPC(hash, senderClientID, stream);
        }
    } //Class
} //Namespace