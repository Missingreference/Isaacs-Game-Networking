#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS0618 // Missing XML comment for publicly visible type or member
using System;
using System.Collections.Generic;
using MLAPI.Transports.Tasks;
using UnityEngine.Networking;
using Isaac.Network;
using Unet = UnityEngine.Networking.NetworkTransport;
using UnityEngine;

namespace Isaac.Network.Transports
{
	public class UnetTransport : NetworkTransport
	{

        public override ChannelType supportedChannelTypes => (ChannelType.Reliable | ChannelType.ReliableFragmentedSequenced | ChannelType.ReliableSequenced | ChannelType.Unreliable | ChannelType.UnreliableSequenced);

        // Inspector / settings
        public int MessageBufferSize = 1024 * 5;
		public int MaxConnections = 100;

		public int ServerListenPort = 7777;
		public int ServerWebsocketListenPort = 8887;
		public bool SupportWebsocket = false;

		// Runtime / state
		private byte[] messageBuffer;
		private WeakReference temporaryBufferReference;

		// Lookup / translation
		private int serverConnectionId;
		private int serverHostId;

        //Unet has their own similar channel implementation so this simply gets the Unet channel equivalent of base NetworkTransport's channel.
        private Dictionary<byte, byte> m_UnetChannelLookup = new Dictionary<byte, byte>(); //key = NetworkTransport channel, value = Unet channel
        private Dictionary<byte, byte> m_TransportChannelLookup = new Dictionary<byte, byte>(); //key = Unet channel, value = NetworkTransport channel

        private SocketTask connectTask;

		public override ulong serverID => GetMLAPIClientID(0, 0, true);

		public override void Send(ulong clientID, ArraySegment<byte> data, byte channel)
		{
			GetUnetConnectionDetails(clientID, out byte hostId, out ushort connectionId);

			byte[] buffer;

			if (data.Offset > 0)
			{
				// UNET cant handle this, do a copy

				if (messageBuffer.Length >= data.Count)
				{
					buffer = messageBuffer;
				}
				else
				{
					if (temporaryBufferReference != null && temporaryBufferReference.IsAlive && ((byte[])temporaryBufferReference.Target).Length >= data.Count)
					{
						buffer = (byte[])temporaryBufferReference.Target;
					}
					else
					{
						buffer = new byte[data.Count];
						temporaryBufferReference = new WeakReference(buffer);
					}
				}

				Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);
			}
			else
			{
				buffer = data.Array;
			}

			/*RelayTransport.*/
			Unet.Send(hostId, connectionId, m_UnetChannelLookup[channel], buffer, data.Count, out byte error);
		}

		public override NetEventType PollEvent(out ulong clientID, out byte channel, out ArraySegment<byte> payload, out float receiveTime)
		{
			NetworkEventType eventType = Unet.Receive(out int hostId, out int connectionId, out int channelID, messageBuffer, messageBuffer.Length, out int receivedSize, out byte error);

			clientID = GetMLAPIClientID((byte)hostId, (ushort)connectionId, false);
            if(channelID > 255 || channelID < 0)
            {
                Debug.LogError("Received invalid Unet channel integer '" + channelID + "'.");
                channel = 255;
            }
            else
            {
                //Convert unet channel to transport channel
                if(!m_TransportChannelLookup.TryGetValue((byte)channelID, out channel))
                {
                    Debug.LogError("Received invalid channel in Unet '" + (byte)channelID + "'.");
                    channel = 255;
                }
            }
			receiveTime = UnityEngine.Time.realtimeSinceStartup;

			NetworkError networkError = (NetworkError)error;

			if (networkError == NetworkError.MessageToLong)
			{
				byte[] tempBuffer;

				if (temporaryBufferReference != null && temporaryBufferReference.IsAlive && ((byte[])temporaryBufferReference.Target).Length >= receivedSize)
				{
					tempBuffer = (byte[])temporaryBufferReference.Target;
				}
				else
				{
					tempBuffer = new byte[receivedSize];
					temporaryBufferReference = new WeakReference(tempBuffer);
				}

				eventType = Unet.Receive(out hostId, out connectionId, out channelID, tempBuffer, tempBuffer.Length, out receivedSize, out error);
				payload = new ArraySegment<byte>(tempBuffer, 0, receivedSize);
			}
			else
			{
				payload = new ArraySegment<byte>(messageBuffer, 0, receivedSize);
			}

			if (connectTask != null && hostId == serverHostId && connectionId == serverConnectionId)
			{
				if (eventType == NetworkEventType.ConnectEvent)
				{
					// We just got a response to our connect request.
					connectTask.Message = null;
					connectTask.SocketError = networkError == NetworkError.Ok ? System.Net.Sockets.SocketError.Success : System.Net.Sockets.SocketError.SocketError;
					connectTask.State = null;
					connectTask.Success = networkError == NetworkError.Ok;
					connectTask.TransportCode = (byte)networkError;
					connectTask.TransportException = null;
					connectTask.IsDone = true;

					connectTask = null;
				}
				else if (eventType == NetworkEventType.DisconnectEvent)
				{
					// We just got a response to our connect request.
					connectTask.Message = null;
					connectTask.SocketError = System.Net.Sockets.SocketError.SocketError;
					connectTask.State = null;
					connectTask.Success = false;
					connectTask.TransportCode = (byte)networkError;
					connectTask.TransportException = null;
					connectTask.IsDone = true;

					connectTask = null;
				}
			}

			if (networkError == NetworkError.Timeout)
			{
				// In UNET. Timeouts are not disconnects. We have to translate that here.
				eventType = NetworkEventType.DisconnectEvent;
			}

			// Translate NetworkEventType to NetEventType
			switch (eventType)
			{
				case NetworkEventType.DataEvent:
					return NetEventType.Data;
				case NetworkEventType.ConnectEvent:
					return NetEventType.Connect;
				case NetworkEventType.DisconnectEvent:
					return NetEventType.Disconnect;
				case NetworkEventType.Nothing:
					return NetEventType.Nothing;
				case NetworkEventType.BroadcastEvent:
					return NetEventType.Nothing;
			}

			return NetEventType.Nothing;
		}

		public override void StartClient()
        {
            if(NetworkManager.Get().enableLogging)
                Debug.Log("Transport Start Client");
            SocketTask task = SocketTask.Working;

			serverHostId = Unet.AddHost(new HostTopology(GetConfig(), 1));
			serverConnectionId = Unet.Connect(serverHostId, address, port, 0, out byte error);

			NetworkError connectError = (NetworkError)error;

			switch (connectError)
			{
				case NetworkError.Ok:
					task.Success = true;
					task.TransportCode = error;
					task.SocketError = System.Net.Sockets.SocketError.Success;
					task.IsDone = false;

					// We want to continue to wait for the successful connect
					connectTask = task;
					break;
				default:
					task.Success = false;
					task.TransportCode = error;
					task.SocketError = System.Net.Sockets.SocketError.SocketError;
					task.IsDone = true;
					break;
			}

			//return task.AsTasks();
		}

		public override void StartServer()
		{
            if(NetworkManager.Get().enableLogging)
                Debug.Log("Transport Start Server");
			HostTopology topology = new HostTopology(GetConfig(), MaxConnections);

			if (SupportWebsocket)
            {
                int websocketHostId = Unet.AddWebsocketHost(topology, ServerWebsocketListenPort);
            }

			int normalHostId = Unet.AddHost(topology, ServerListenPort);

			//return SocketTask.Done.AsTasks();
		}

		public override void DisconnectRemoteClient(ulong clientId)
        {
            if(NetworkManager.Get().enableLogging)
                Debug.Log("Transport Disconnect Remote Client");
            GetUnetConnectionDetails(clientId, out byte hostId, out ushort connectionId);

			Unet.Disconnect((int)hostId, (int)connectionId, out byte error);
		}

		public override void DisconnectLocalClient()
        {
            if(NetworkManager.Get().enableLogging)
                Debug.Log("Transport Disconnect Local Client");
            Unet.Disconnect(serverHostId, serverConnectionId, out byte error);
		}

		public override ulong GetCurrentRtt(ulong clientId)
		{
			GetUnetConnectionDetails(clientId, out byte hostId, out ushort connectionId);

            return (ulong)Unet.GetCurrentRTT((int)hostId, (int)connectionId, out byte error);
        }

		public override void Shutdown()
		{
            m_UnetChannelLookup.Clear();
            m_TransportChannelLookup.Clear();
            Unet.Shutdown();
		}

		public override void Init()
		{
            if(NetworkManager.Get().enableLogging)
                Debug.Log("Unet init called");
			messageBuffer = new byte[MessageBufferSize];

			Unet.Init();
		}

		public ulong GetMLAPIClientID(byte hostId, ushort connectionId, bool isServer)
		{
			if (isServer)
			{
				return 0;
            }
            return ((ulong)connectionId | (ulong)hostId << 16) + 1;
        }

		public void GetUnetConnectionDetails(ulong clientId, out byte hostId, out ushort connectionId)
		{
			if (clientId == 0)
			{
				hostId = (byte)serverHostId;
				connectionId = (ushort)serverConnectionId;
			}
			else
			{
				hostId = (byte)((clientId - 1) >> 16);
				connectionId = (ushort)((clientId - 1));
			}
		}

		public ConnectionConfig GetConfig()
		{
			ConnectionConfig config = new ConnectionConfig();

            int channelsLeft = channelCount;
            for(byte i = 0; i < channelCount; i++)
            {
                TransportChannel transportChannel = TryGetTransportChannel(i);
                if(transportChannel == null) continue;

                byte unetChannel = config.AddChannel(ChannelTypeToQosType(transportChannel.channelType));
                m_UnetChannelLookup.Add(i, unetChannel);
                m_TransportChannelLookup.Add(unetChannel, i);

                channelsLeft--;
                if(channelsLeft == 0) break;
            }

			return config;
		}

        public QosType ChannelTypeToQosType(ChannelType channelType)
        {
            switch(channelType)
            {
                case ChannelType.Unreliable:
                    return QosType.Unreliable;
                case ChannelType.Reliable:
                    return QosType.Reliable;
                case ChannelType.ReliableSequenced:
                    return QosType.ReliableSequenced;
                case ChannelType.ReliableFragmentedSequenced:
                    return QosType.ReliableFragmentedSequenced;
                case ChannelType.UnreliableSequenced:
                    return QosType.UnreliableSequenced;
            }
            return QosType.Unreliable;
        }
	}
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CS0618 // Missing XML comment for publicly visible type or member