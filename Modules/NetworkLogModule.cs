using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

using UnityEngine;

using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using BitStream = MLAPI.Serialization.BitStream;

using Isaac.Network.Messaging;

namespace Isaac.Network.Development
{
    /// <summary>
    /// This debug module is used for sending and receiving Unity logs across the network. Server can send it's logs to clients and clients can send their logs to the server.
    /// Server can manage whether clients can toggle server logs to be sent to the client. 
    /// </summary>
    public class NetworkLogModule : NetworkModule
    {
        public byte networkLogMessageType { get; private set; } = 255;
        public byte networkToggleLogMessageType { get; private set; } = 255;

        public override Type[] dependencies => new Type[] { typeof(NetworkMessageHandler) };

        /// <summary>
        /// Whether this build will send UnityEngine log events across the network.
        /// </summary>
        public bool sendLogEvents = true;

        /// <summary>
        /// When sending logs, whether to send the stack trace of log message. Including the stacktrace will increase the size of the message and cause network lag.
        /// </summary>
        public bool includeStacktrace = false;

        /// <summary>
        /// Whether clients are allowed to toggle if they receive log events or not. Server only.
        /// </summary>
        public bool clientsAllowedToToggle = true;

        /// <summary>
        /// Similar to UnityEngine.Application.LogCallback but with the difference that it the callback will be identified by the client ID.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="condition"></param>
        /// <param name="stackTrace"></param>
        /// <param name="type"></param>
        public delegate void NetworkLogCallback(ulong clientID, string condition, string stackTrace, LogType type);

        /// <summary>
        /// Callback for when another build across the network has a log of any kind. If this build is a client(not server) it will only receive log events from the server and not from other clients.
        /// DO NOT CALL UnityEngine.Debug.Log AND OTHER LOG FUNCTIONS FROM THIS EVENT IF THIS BUILD IS EXPECTED TO SEND LOG EVENTS. THIS WILL CAUSE AN INFINITE LOOP ACROSS THE NETWORK.
        /// </summary>
        public event NetworkLogCallback onReceivedNetworkLog
        {
            add
            {
                m_OnReceivedNetworkLog += value;
            }
            remove
            {
                m_OnReceivedNetworkLog -= value;
            }
        }

        private event NetworkLogCallback m_OnReceivedNetworkLog;

        private readonly List<ulong> m_ClientsReceivingLogEvents = new List<ulong>();
        private NetworkMessageHandler m_NetworkMessageHandler;

        public NetworkLogModule()
        {

        }

        public override void Init(NetworkModule[] loadedDependencies)
        {
            if(m_NetworkMessageHandler == null)
            {
                m_NetworkMessageHandler = loadedDependencies[0] as NetworkMessageHandler;
                networkLogMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_LOG", HandleNetworkLog, NetworkMessageHandler.NetworkMessageReceiver.Both);
                networkToggleLogMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_TOGGLE_LOG", HandleNetworkToggleLog, NetworkMessageHandler.NetworkMessageReceiver.Both);
            }
            Application.logMessageReceived += OnLog;
            if(networkManager.enableLogging)
            {
                Debug.LogWarning("If Network Manager logging is enabled, be careful that this may cause an infinite loop across the network of logging if both the server and client are sending logs to eachother.");
            }
        }

        public override void Shutdown()
        {
            Application.logMessageReceived -= OnLog;
            m_OnReceivedNetworkLog = null;
        }

        public override void OnClientDisconnect(ulong clientID)
        {
            if(networkManager.isServer)
                m_ClientsReceivingLogEvents.Remove(clientID);
        }

        /// <summary>
        /// Send a message to the target client ID whether they should send logs to this client. If this build is a client (not server) and is not allowed to toggle on the server, it will fail silently.
        /// Cannot target other clients if this build is a client (not server).
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="sendLogs"></param>
        /// <param name="includeStacktraceInMessage"></param>
        public void ToggleRemoteTarget(ulong clientID, bool sendLogs, bool includeStacktraceInMessage)
        {
            if(networkManager.isServer)
            {
                if(clientID == networkManager.serverID)
                {
                    sendLogEvents = sendLogs;
                    includeStacktrace = includeStacktraceInMessage;
                    return;
                }

                if(!networkManager.connectedClients.Contains(clientID))
                {
                    Debug.LogError("Tried to toggle remote target for sending logs but the client ID '" + clientID + "' is not in the connected clients list.");
                    return;
                }
            }
            else
            {
                if(clientID != networkManager.serverID)
                {
                    Debug.LogError("Clients can only toggle network logs for the server. Tried to toggle non-server client ID '" + clientID + "'.");
                    return;
                }
            }

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteBool(sendLogs);
                    MessageSender.Send(clientID, networkToggleLogMessageType, "NETWORK_INTERNAL", stream);
                }
            }
        }

        private void OnLog(string condition, string stackTrace, LogType logType)
        {
            //DO NOT CALL Debug.Log in this function or it will cause an infinite loop!
            //Wait until this network is ready to log.
            if(!sendLogEvents || !networkManager.isRunning || (!networkManager.isServer && !networkManager.isConnected)) return;

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteString(condition);
                    writer.WriteBool(includeStacktrace);
                    if(includeStacktrace)
                        writer.WriteString(stackTrace);
                    writer.WriteByte((byte)logType);
                    if(networkManager.isServer)
                    {
                        //Send to all clients that are expecting it.
                        MessageSender.SendToSpecific(m_ClientsReceivingLogEvents, networkLogMessageType, "NETWORK_INTERNAL", stream);
                    }
                    else
                    {
                        //Send to server
                        MessageSender.Send(networkManager.serverID, networkLogMessageType, "NETWORK_INTERNAL", stream);
                    }
                }
            }
        }

        private void HandleNetworkLog(ulong sendingClientID, Stream stream, float receiveTime)
        {
            //DO NOT* CALL Debug.Log or other log functions in this function. *See onReceivedNetworkLog summary comments.
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                m_OnReceivedNetworkLog?.Invoke(sendingClientID, reader.ReadString().ToString(), reader.ReadBool() ? reader.ReadString().ToString() : string.Empty, (LogType)reader.ReadByte());
            }
        }

        private void HandleNetworkToggleLog(ulong sendingClientID, Stream stream, float receiveTime)
        {
            if(networkManager.isServer && !clientsAllowedToToggle)
            {
                //Fail silently.
                return;
            }

            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                if(networkManager.isServer)
                {
                    //Toggling sendLogEvents on the server will act as a global toggle for all clients so instead
                    if(reader.ReadBool())
                    {
                        if(!m_ClientsReceivingLogEvents.Contains(sendingClientID))
                            m_ClientsReceivingLogEvents.Add(sendingClientID);
                    }
                    else
                    {
                        m_ClientsReceivingLogEvents.Remove(sendingClientID);
                    }
                    //Any client can toggle includeStacktrace and will affect all other clients
                    includeStacktrace = reader.ReadBool();
                }
                else
                {
                    sendLogEvents = reader.ReadBool();
                    includeStacktrace = reader.ReadBool();
                }
            }
        }
    }
}