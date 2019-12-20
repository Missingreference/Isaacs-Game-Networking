using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using BitStream = MLAPI.Serialization.BitStream;
using MLAPI.Messaging;
using MLAPI.Security;
using MLAPI.Internal;

using Isaac.Network.Connection;
using Isaac.Network.Spawning;
using Isaac.Network.Messaging;
using Isaac.Network.SceneManagement;
using Isaac.Network.Development;

namespace Isaac.Network
{
	public class NetworkManager : MonoBehaviour
	{

		static public NetworkManager Get()
		{
			if (m_NetworkManager == null)
			{
				m_NetworkManager = FindObjectOfType<NetworkManager>();
			}
			return m_NetworkManager;
		}

		static private NetworkManager m_NetworkManager = null;

        /// <summary>
        /// Whether this Network Manager is the server.
        /// </summary>
		public bool isServer { get; private set; }
        /// <summary>
        /// Whether this Network Manager is a client.
        /// </summary>
		public bool isClient { get; private set; }
        /// <summary>
        /// Whether this Network Manager is a server AND client.
        /// </summary>
		public bool isHost => isServer && isClient;
        /// <summary>
        /// Whether this Network Manager is running regardless of connection.
        /// </summary>
		public bool isRunning => isServer || isClient;
        /// <summary>
        /// Whether this client is connected. Only valid for (non-server) client.
        /// </summary>
		public bool isConnected { get; private set; }
		/// <summary>
		/// Gets the networkID of the server
		/// </summary>
		public ulong serverID => transport != null ? transport.serverID : throw new NullReferenceException("The transport is null");
		/// <summary>
		/// The networking ID of this client. If this client is a server, it simply returns the server ID.
		/// </summary>
		public ulong clientID
		{
			get
			{
				if (isServer) return serverID;
				else return m_ClientID;
			}
			private set
			{
				m_ClientID = value;
			}
		}

		public NetworkTransport transport
		{
			get
			{
				return m_Transport;
			}
			set
			{
				if (isRunning)
				{
					Debug.LogWarning("Cannot change transport while the Network Manager is running.");
					return;
				}
				m_Transport = value;
			}
		}

		public NetworkConfig config
		{
			get
			{
				return m_Config;
			}
			set
			{
				if (isRunning)
				{
					Debug.LogWarning("Cannot change config while the Network Manager is running.");
					return;
				}
				m_Config = value;
			}
		}

		/// <summary>
		/// A synchronized time, represents the time in seconds since the server application started. Is replicated across all clients
		/// </summary>
		public float NetworkTime => Time.unscaledTime + m_CurrentNetworkTimeOffset;

        /// <summary>
        /// Gets a list of connected clients
        /// </summary>
        public readonly List<ulong> connectedClients = new List<ulong>();
		/// <summary>
		/// Gets a dictionary of the clients that have been accepted by the transport but are still pending by the MLAPI.
		/// </summary>
		public readonly Dictionary<ulong, PendingClient> pendingClients = new Dictionary<ulong, PendingClient>();

		//Events
        /// <summary>
        /// Called on the server when a client connects.
        /// //TODO: Called on the client when it connects to the server.
        /// </summary>
		public Action<ulong> onClientConnect;
        /// <summary>
        /// Called on the server when a client disconnects.
        /// </summary>
		public Action<ulong> onClientDisconnect;
        /// <summary>
        /// Called after the Network Manager and transport initializes.
        /// </summary>
        public Action onInitialize;
        /// <summary>
        /// Called after the Network Manager and transport shutsdown.
        /// </summary>
        public Action onShutdown;

        public bool enableLogging = false;

        //Private
        private readonly List<NetworkModule> m_Modules = new List<NetworkModule>();
        private NetworkBehaviourManager m_BehaviourManager;
        private NetworkMessageHandler m_MessageHandler;

		private ulong m_ClientID;
		private float m_NetworkTimeOffset;
		private float m_CurrentNetworkTimeOffset;
		private bool m_NetworkTimeInitialized;
		private NetworkTransport m_Transport = null;
		private NetworkConfig m_Config = null;

        //Channels
        private byte m_NetworkInternalChannel = NetworkTransport.INVALID_CHANNEL;
        private byte m_TimeSyncChannel = NetworkTransport.INVALID_CHANNEL;

		void Awake()
		{
            //Static check
            if(Get() != this)
			{
				Debug.LogError("Cannot have more than 1 NetworkManager to exist. Destroying...");
				Destroy(this);
                return;
			}
		}

		void Start()
        {
            if(Debug.isDebugBuild && !Application.isEditor) Debug.LogError("Init");
        }

        private float m_LastReceiveTickTime;
        private float m_LastEventTickTime;
        private float m_EventOvershootCounter;
        private float m_LastTimeSyncTime;
        void Update()
		{

            if(isRunning)
            {
                if((NetworkTime - m_LastReceiveTickTime >= (1f / config.receiveTickrate)) || config.receiveTickrate <= 0)
                {
                    NetEventType eventType;
                    int processedEvents = 0;
                    do
                    {
                        processedEvents++;
                        eventType = transport.PollEvent(out ulong clientId, out byte channel, out ArraySegment<byte> payload, out float receiveTime);
                        HandleRawTransportPoll(eventType, clientId, channel, payload, receiveTime);

                        // Only do another iteration if: there are no more messages AND (there is no limit to max events or we have processed less than the maximum)
                    } while(isRunning && (eventType != NetEventType.Nothing && (config.maxReceiveEventsPerTickRate <= 0 || processedEvents < config.maxReceiveEventsPerTickRate)));
                    m_LastReceiveTickTime = NetworkTime;
                }

                if(!isRunning)
                {
                    // If we get disconnected in the previous poll. IsListening will be set to false.
                    return;
                }

                if(((NetworkTime - m_LastEventTickTime >= (1f / config.eventTickrate))))
                {

                    if(isServer)
                    {
                        m_EventOvershootCounter += ((NetworkTime - m_LastEventTickTime) - (1f / config.eventTickrate));
                        //LagCompensationManager.AddFrames();
                        ResponseMessageManager.CheckTimeouts();
                    }

                    if(config.enableNetworkedVar)
                    {
                        // Do NetworkedVar updates
                        //NetworkedObject.NetworkedBehaviourUpdate();
                    }

                    if(isServer)
                    {
                        m_LastEventTickTime = NetworkTime;
                    }

                }
                else if(isServer && m_EventOvershootCounter >= ((1f / config.eventTickrate)))
                {
                    //We run this one to compensate for previous update overshoots.
                    m_EventOvershootCounter -= (1f / config.eventTickrate);
                    //LagCompensationManager.AddFrames();
                }

                if(isServer && config.enableTimeResync && NetworkTime - m_LastTimeSyncTime >= config.timeResyncInterval)
                {
                    SyncTime();
                    m_LastTimeSyncTime = NetworkTime;
                }

                if(!Mathf.Approximately(m_NetworkTimeOffset, m_CurrentNetworkTimeOffset))
                {
                    // Smear network time adjustments by no more than 200ms per second.  This should help code deal with
                    // changes more gracefully, since the network time will always flow forward at a reasonable pace.
                    float maxDelta = Mathf.Max(0.001f, 0.2f * Time.unscaledDeltaTime);
                    m_CurrentNetworkTimeOffset += Mathf.Clamp(m_NetworkTimeOffset - m_CurrentNetworkTimeOffset, -maxDelta, maxDelta);
                }
            }
        }

        void OnDestroy()
        {
            if(Get() == null || Get() != this) return;

            if(isHost)
                StopHost();
            else if(isServer)
                StopServer();
            else if(isClient)
                StopClient();
            //Shutdown();
        }

        private void Shutdown()
        {
            Debug.Log("Shutdown called");
            if(enableLogging)
                Debug.Log("Shutdown called");

            bool initialized = isRunning;

            isServer = false;
            isClient = false;

            if(transport != null)
            {
                transport.OnTransportEvent -= HandleRawTransportPoll;
                transport.Shutdown();
            }

            //Remove from DontDestroyOnLoad
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());

            //onShutdown event should only be called when on onInitilize is called.
            if(initialized)
            {
                for(int i = 0; i < m_Modules.Count; i++)
                {
                    m_Modules[i].Shutdown();
                }
                onShutdown?.Invoke();
            }
        }

        public void StartServer()
		{
            if(enableLogging)
			    Debug.Log("StartServer called");
			if(isRunning)
			{
				if (isHost)
					Debug.LogWarning("Cannot start server. Host is already running. Call StopHost before trying to start server.");
				else if (isServer)
					Debug.LogWarning("Cannot start server. Server is already running. Call StopServer before trying to start server.");
				else if (isClient)
					Debug.LogWarning("Cannot start server. Client is already running. Call StopClient before trying to start server.");
				return;
			}

			Init();

			transport.StartServer();

			isServer = true;

            for(int i = 0; i < m_Modules.Count; i++)
            {
                m_Modules[i].OnNetworkReady();
            }

            onInitialize?.Invoke();
        }

		public void StartClient()
		{
            if(enableLogging)
                Debug.Log("StartClient called");
			if(isRunning)
			{
				if (isHost)
					Debug.LogWarning("Cannot start client. Host is already running. Call StopHost before trying to start client.");
				else if (isServer)
					Debug.LogWarning("Cannot start client. Server is already running. Call StopServer before trying to start client.");
				else if (isClient)
					Debug.LogWarning("Cannot start client. Client is already running. Call StopClient before trying to start client.");
				return;
			}

			Init();

			transport.StartClient();

			isClient = true;

            for(int i = 0; i < m_Modules.Count; i++)
            {
                m_Modules[i].OnNetworkReady();
            }

            onInitialize?.Invoke();
        }

		public void StartHost()
		{
            if(enableLogging)
                Debug.Log("StartHost called");
			if (isRunning)
			{
				if (isHost)
					Debug.LogWarning("Cannot start host. Host is already running. Call StopHost before trying to start host.");
				else if (isServer)
					Debug.LogWarning("Cannot start host. Server is already running. Call StopServer before trying to start host.");
				else if (isClient)
					Debug.LogWarning("Cannot start host. Client is already running. Call StopClient before trying to start host.");
				return;
			}

            //Optional Connection Approval (MLAPI Line 541)

			Init();

            transport.StartServer();

			isServer = true;
			isClient = true;

			ulong hostID = transport.serverID;

			/*connectedClientsDictionary.Add(hostID, new NetworkedClient()
			{
				clientID = hostID
			});*/

            connectedClients.Add(hostID);//connectedClientsDictionary[hostID]);

            for(int i = 0; i < m_Modules.Count; i++)
            {
                m_Modules[i].OnNetworkReady();
            }

            onInitialize?.Invoke();
		}

		public void StopServer()
		{
            if(enableLogging)
                Debug.Log("StopServer called");

            HashSet<ulong> disconnectedIDs = new HashSet<ulong>();
            //(MLAPI COMMENT not Isaac) Don't know if I have to disconnect the clients. I'm assuming the NetworkTransport does all the cleaning on shtudown. But this way the clients get a disconnect message from server (so long it doesn't get lost)

            //foreach(KeyValuePair<ulong, NetworkedClient> pair in connectedClientsDictionary)
            for(int i = 0; i < connectedClients.Count; i++)
            {
                if(!disconnectedIDs.Contains(connectedClients[i]))
                {
                    disconnectedIDs.Add(connectedClients[i]);

                    if(connectedClients[i] == transport.serverID)
                        continue;

                    transport.DisconnectRemoteClient(connectedClients[i]);
                }
            }

            foreach(KeyValuePair<ulong, PendingClient> pair in pendingClients)
            {
                if(!disconnectedIDs.Contains(pair.Key))
                {
                    disconnectedIDs.Add(pair.Key);
                    if(pair.Key == transport.serverID)
                        continue;

                    transport.DisconnectRemoteClient(pair.Key);
                }
            }

            Shutdown();
        }

		public void StopClient()
		{
            if(enableLogging)
                Debug.Log("StopClient called");
            transport.DisconnectLocalClient();
            isConnected = false;
            Shutdown();
        }

		public void StopHost()
		{
            if(enableLogging)
                Debug.Log("StopHost called");

            StopServer();
            //We don't stop client since we dont actually have a transport connection to our own host. We just handle host messages directly in the MLAPI
        }

        /// <summary>
        /// Disconnects the remote client. Server only.
        /// </summary>
        /// <param name="targetClientID">The ClientId to disconnect</param>
        public void DisconnectClient(ulong targetClientID)
		{
			if (!isServer)
			{
				Debug.LogError("Only server can disconnect remote clients. Use StopClient instead.");
				return;
			}

			if (pendingClients.ContainsKey(targetClientID))
				pendingClients.Remove(targetClientID);

			for (int i = connectedClients.Count - 1; i > -1; i--)
			{
				if (connectedClients[i] == targetClientID)
					connectedClients.RemoveAt(i);
			}

			transport.DisconnectRemoteClient(targetClientID);
		}

        #region Module Management
        
        private T LoadModule<T>() where T : NetworkModule
        {
            //Check if the module is already loaded
            for(int i = 0; i < m_Modules.Count; i++)
            {
                if(m_Modules[i] is T)
                {
                    Debug.LogError("Module '" + typeof(T).Name + "' is already loaded. Only one instance of a module can be loaded.");
                    return null;
                }
            }

            T loadedModule = Activator.CreateInstance<T>();
            m_Modules.Add(loadedModule);

            //Load dependencies
            for(int i = 0; i < loadedModule.dependencies.Length; i++)
            {
                Type dependencyType = loadedModule.dependencies[i];
                if(!dependencyType.IsSubclassOf(typeof(NetworkModule)))
                {
                    Debug.LogWarning("Type '" + dependencyType.Name + "' is not a NetworkModule type and cannot be used as a dependency.");
                    continue;
                }
                
                //Check if its already loaded
                bool found = false;
                for(int h = 0; h < m_Modules.Count; h++)
                {
                    if(m_Modules[h].GetType() == dependencyType)
                    {
                        found = true;
                        break;
                    }
                }
                if(found) continue;

                //Not loaded, use reflection to call this generic method since we can only use the Type object
                MethodInfo loadModuleInfo = this.GetType().GetMethod("LoadModule", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if(loadModuleInfo == null)
                {
                    loadModuleInfo = this.GetType().GetMethod("LoadModule", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                }
                //if(enableLogging)
                //Debug.Log("Loading Dependancy: " + dependencyType.Name);
                loadModuleInfo.MakeGenericMethod(dependencyType).Invoke(this, null);
            }

            return loadedModule;
        }

        /// <summary>
        /// Trys to retrieve the loaded Network Module. If the inputted Network Module type is not loaded
        /// </summary>
        /// <typeparam name="T">The type of Network Module to try to retrieve from the Network Manager.</typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : NetworkModule
        {
            for(int i = 0; i < m_Modules.Count; i++)
            {
                if(m_Modules[i] is T)
                    return (T)m_Modules[i];
            }
            return null;
        }

        #endregion

        private void Init()
		{
            if(enableLogging)
                Debug.Log("Init Called");

            clientID = 0;
            m_NetworkTimeOffset = 0f;
            m_CurrentNetworkTimeOffset = 0f;
            m_NetworkTimeInitialized = false;
            m_LastEventTickTime = 0f;
            m_LastReceiveTickTime = 0f;
            m_EventOvershootCounter = 0f;
            pendingClients.Clear();
            connectedClients.Clear();

            ResponseMessageManager.Clear();

            //Load Modules
            if(m_MessageHandler == null)
            {
                LoadRequiredModules();
                m_MessageHandler = GetModule<NetworkMessageHandler>();
                m_BehaviourManager = GetModule<NetworkBehaviourManager>();

                //Register required message types.
                m_MessageHandler.RegisterMessageType(MessageType.NETWORK_CONNECTION_REQUEST.ToString(), HandleConnectionRequest, NetworkMessageHandler.NetworkMessageReceiver.Server);
                m_MessageHandler.RegisterMessageType(MessageType.NETWORK_CONNECTION_APPROVED.ToString(), HandleConnectionApproved, NetworkMessageHandler.NetworkMessageReceiver.Client);
                m_MessageHandler.RegisterMessageType(MessageType.NETWORK_TIME_SYNC.ToString(), HandleTimeSync, NetworkMessageHandler.NetworkMessageReceiver.Client);
            }

            //Init Modules
            for(int i = 0; i < m_Modules.Count; i++)
            {
                //TODO This allocation should be reused instead of being cleaned up everytime
                NetworkModule[] dependantModules = new NetworkModule[m_Modules[i].dependencies.Length];
                int modulesLeft = dependantModules.Length;
                if(modulesLeft > 0)
                {
                    for(int j = 0; j < m_Modules[i].dependencies.Length; j++)
                    {
                        for(int h = 0; h < m_Modules.Count; h++)
                        {
                            if(m_Modules[h].GetType() == m_Modules[i].dependencies[j])
                            {
                                dependantModules[j] = m_Modules[h];
                                modulesLeft--;
                                if(modulesLeft == 0) break;
                            }
                        }
                        if(modulesLeft == 0) break;
                    }
                }
                m_Modules[i].Init(dependantModules);
            }

            if(config == null)
			{
				Debug.LogError("Failed to Init Network Manager. No config is set.");
				return;
			}
			if(transport == null)
			{
				Debug.LogError("Failed to Init Network Manager. No transport is set.");
				return;
			}

            DontDestroyOnLoad(gameObject);

			transport.OnTransportEvent += HandleRawTransportPoll;

			transport.Init();
		}

        private void LoadRequiredModules()
        {
            //Network Message Handler
            if(GetModule<NetworkMessageHandler>() == null)
            {
                LoadModule<NetworkMessageHandler>();
            }

            //Network Behaviour Manager
            if(GetModule<NetworkBehaviourManager>() == null)
            {
                LoadModule<NetworkBehaviourManager>();
            }

            //Scene Manager
            if(GetModule<NetworkSceneManager>() == null)
            {
                LoadModule<NetworkSceneManager>();
            }

            
            if(Debug.isDebugBuild)
            {
                //Network Log Module
                if(GetModule<NetworkLogModule>() == null)
                {
                    LoadModule<NetworkLogModule>();
                }
            }
        }

        private void HandleRawTransportPoll(NetEventType eventType, ulong sendingClientID, byte channel, ArraySegment<byte> payload, float receiveTime)
        {
            switch (eventType)
			{
				case NetEventType.Connect:

					if (isServer)
					{
						pendingClients.Add(sendingClientID, new PendingClient()
						{
							clientID = sendingClientID,
							connectionState = PendingClient.State.PendingConnection
						});
						StartCoroutine(ApprovalTimeout(sendingClientID));
					}
					else
					{
                        if(enableLogging)
                            Debug.Log("Connected");

						SendConnectionRequest();
						StartCoroutine(ApprovalTimeout(sendingClientID));
					}
					break;
				case NetEventType.Data:
                    if(enableLogging)
                        Debug.Log($"Incoming Data From {sendingClientID} : {payload.Count} bytes");

					HandleIncomingData(sendingClientID, channel, payload, receiveTime);
					break;
				case NetEventType.Disconnect:
                    if(enableLogging)
                        Debug.Log("Client disconnect: " + sendingClientID);

					if (isServer)
						OnClientDisconnectFromServer(sendingClientID);
					else
					{
						isConnected = false;
						StopClient();
					}

                    for(int i = 0; i < m_Modules.Count; i++)
                    {
                        m_Modules[i].OnClientDisconnect(sendingClientID);
                    }

					onClientDisconnect?.Invoke(sendingClientID);
					break;
			}
		}

		private readonly BitStream inputStreamWrapper = new BitStream(new byte[0]);

		private void HandleIncomingData(ulong sendingClientID, byte channel, ArraySegment<byte> data, float receiveTime)
		{
            if(enableLogging)
                Debug.Log("Unwrapping Data Header");

			inputStreamWrapper.SetTarget(data.Array);
			inputStreamWrapper.SetLength(data.Count + data.Offset);
			inputStreamWrapper.Position = data.Offset;

			using (BitStream messageStream = MessagePacker.UnwrapMessage(inputStreamWrapper, sendingClientID, out byte messageType, out SecuritySendFlags security))
			{
				if (messageStream == null)
				{
					Debug.LogError("Message unwrap could not be completed. Was the header corrupt? Crypto error?");
					return;
				}
				else if (messageType == (byte)MessageType.INVALID)
				{
					Debug.LogError("Message unwrap read an invalid messageType");
					return;
				}

				uint headerByteSize = (uint)Arithmetic.VarIntSize(messageType);

                if(enableLogging)
                    Debug.Log("Data Header: messageType=" + m_MessageHandler.GetMessageName(messageType) + "(" + messageType + ")");

				// Client tried to send a network message that was not the connection request before they were accepted
				if (pendingClients.ContainsKey(sendingClientID) && pendingClients[sendingClientID].connectionState == PendingClient.State.PendingConnection && messageType != (byte)MessageType.NETWORK_CONNECTION_REQUEST)
				{
					Debug.LogWarning("Message received from clientID " + sendingClientID + " before it has been accepted. Message type: (" + messageType.ToString() + ") " + m_MessageHandler.GetMessageName(messageType));
					return;
				}

                //Handle Message
                m_MessageHandler.HandleMessage(sendingClientID, messageType, messageStream, receiveTime);
			}
		}

		private void UpdateNetworkTime(ulong clientId, float netTime, float receiveTime, bool onlyIfNotInitialized = false)
		{
			if (onlyIfNotInitialized && m_NetworkTimeInitialized)
				return;
			float rtt = transport.GetCurrentRtt(clientId) / 1000f;
			m_NetworkTimeOffset = netTime - receiveTime + rtt / 2f;
			if (!m_NetworkTimeInitialized)
			{
				m_CurrentNetworkTimeOffset = m_NetworkTimeOffset;
				m_NetworkTimeInitialized = true;
			}
            if(enableLogging)
                Debug.Log($"Received network time {netTime}, RTT to server is {rtt}, setting offset to {m_NetworkTimeOffset} (delta {m_NetworkTimeOffset - m_CurrentNetworkTimeOffset})");
		}

		private IEnumerator ApprovalTimeout(ulong connectingClientID)
		{
			float timeStarted = NetworkTime;
			//We yield every frame incase a pending client disconnects and someone else gets its connection id
			while (NetworkTime - timeStarted < config.clientConnectionBufferTimeout && pendingClients.ContainsKey(connectingClientID))
			{
				yield return null;
			}

			if (pendingClients.ContainsKey(connectingClientID) && !connectedClients.Contains(connectingClientID))//connectedClientsDictionary.ContainsKey(clientId))
            {
                //Timeout
                if(enableLogging)
                    Debug.Log("Client " + connectingClientID + " Handshake Timed Out");
				DisconnectClient(connectingClientID);
			}
		}

		private void SendConnectionRequest()
		{
			using (PooledBitStream stream = PooledBitStream.Get())
			{
				using (PooledBitWriter writer = PooledBitWriter.Get(stream))
				{
					writer.WriteUInt64Packed(config.GetConfig());

					//if (NetworkConfig.ConnectionApproval)
					//	writer.WriteByteArray(NetworkConfig.ConnectionData);
				}

				MessageSender.Send(serverID, MessageType.NETWORK_CONNECTION_REQUEST, "NETWORK_INTERNAL", stream);
			}
		}

		internal void OnClientDisconnectFromServer(ulong disconnectingClientID)
        {
            pendingClients.Remove(disconnectingClientID);
            connectedClients.Remove(disconnectingClientID);
		}

		private void SyncTime()
		{
            if(enableLogging)
                Debug.Log("Syncing Time To Clients");
			using (PooledBitStream stream = PooledBitStream.Get())
			{
				using (PooledBitWriter writer = PooledBitWriter.Get(stream))
				{
					writer.WriteSinglePacked(Time.realtimeSinceStartup);
					MessageSender.SendToAll(MessageType.NETWORK_TIME_SYNC, "NETWORK_TIME_SYNC", stream);
				}
			}
            if(enableLogging)
                Debug.Log("Time synced");
		}

		internal void HandleApproval(ulong sendingClientID, bool approved)
		{
			if (approved)
			{
				//Inform new client it got approved
				if(pendingClients.ContainsKey(sendingClientID))
					pendingClients.Remove(sendingClientID);
				connectedClients.Add(sendingClientID);

				// This packet is unreliable, but if it gets through it should provide a much better sync than the potentially huge approval message.
				SyncTime();

				using (PooledBitStream stream = PooledBitStream.Get())
				{
					using (PooledBitWriter writer = PooledBitWriter.Get(stream))
					{
						writer.WriteUInt64Packed(sendingClientID);

						writer.WriteSinglePacked(Time.realtimeSinceStartup);

						writer.WriteUInt32Packed(0);

						MessageSender.Send(sendingClientID, MessageType.NETWORK_CONNECTION_APPROVED, "NETWORK_INTERNAL", stream);

                        for(int i = 0; i < m_Modules.Count; i++)
                        {
                            m_Modules[i].OnClientConnect(sendingClientID);
                        }

						onClientConnect?.Invoke(sendingClientID);
					}
				}
			}
			else
			{
				if (pendingClients.ContainsKey(sendingClientID))
					pendingClients.Remove(sendingClientID);

				transport.DisconnectRemoteClient(sendingClientID);
			}
		}


        private void HandleConnectionRequest(ulong sendingClientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong configHash = reader.ReadUInt64Packed();
                if(!NetworkManager.Get().config.CompareConfig(configHash))
                {
                    Debug.LogWarning("Network configuration mismatch. The configuration between the server and client does not match.");
                    NetworkManager.Get().DisconnectClient(sendingClientID);
                    return;
                }

                NetworkManager.Get().HandleApproval(sendingClientID, true);
            }
        }

        private void HandleConnectionApproved(ulong sendingClientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                sendingClientID = reader.ReadUInt64Packed();

                float netTime = reader.ReadSinglePacked();
                UpdateNetworkTime(sendingClientID, netTime, receiveTime, true);
                //networkManager.connectedClientsDictionary.Add(networkManager.clientID, new NetworkedClient { clientID = networkManager.clientID });
                connectedClients.Add(sendingClientID);
                isConnected = true;

                for(int i = 0; i < m_Modules.Count; i++)
                {
                    m_Modules[i].OnClientConnect(sendingClientID);
                }
                onClientConnect?.Invoke(sendingClientID);
            }
        }

        private void HandleTimeSync(ulong sendingClientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                float netTime = reader.ReadSinglePacked();
                UpdateNetworkTime(sendingClientID, netTime, receiveTime);
            }
        }

    } //Class
} //Namespace
