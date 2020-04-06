using System;
using System.Collections.Generic;
using ENet;
using Elanetic.Network;

namespace Elanetic.Network.Transports
{
	public class EnetTransport : NetworkTransport
	{
        //public override bool IsSupported => UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WebGLPlayer;

        public override ChannelType supportedChannelTypes => (ChannelType.ReliableFragmentedSequenced | ChannelType.ReliableSequenced | ChannelType.Unreliable | ChannelType.UnreliableSequenced);

        public int MaxClients = 100;
		public int MessageBufferSize = 1024 * 5;

        //Enet Settings
		public uint PingInterval = 500;
		public uint TimeoutLimit = 32;
		public uint TimeoutMinimum = 5000;
		public uint TimeoutMaximum = 30000;


		// Runtime / state
		private byte[] messageBuffer;
		private WeakReference temporaryBufferReference;


		private readonly Dictionary<uint, Peer> connectedEnetPeers = new Dictionary<uint, Peer>();

		private Host host;

		private uint serverPeerId;

		public override ulong serverID => GetMLAPIClientID(0, true);

		public override void Send(ulong clientID, ArraySegment<byte> data, byte channel)
		{
			Packet packet = default(Packet);

            packet.Create(data.Array, data.Offset, data.Count, ChannelTypeToPacketFlags(GetChannelType(channel)));

			GetEnetConnectionDetails(clientID, out uint peerId);

			connectedEnetPeers[peerId].Send(channel, ref packet);
		}

		public override NetEventType PollEvent(out ulong clientID, out byte channel, out ArraySegment<byte> payload, out float receiveTime)
		{
			Event @event;

			if (host.CheckEvents(out @event) <= 0)
			{
				if (host.Service(0, out @event) <= 0)
				{
					clientID = 0;
                    channel = INVALID_CHANNEL;
					payload = new ArraySegment<byte>();
					receiveTime = UnityEngine.Time.realtimeSinceStartup;

					return NetEventType.Nothing;
				}
			}

			clientID = GetMLAPIClientID(@event.Peer.ID, false);

			switch (@event.Type)
			{
				case EventType.None:
					{
                        channel = INVALID_CHANNEL;
						payload = new ArraySegment<byte>();
						receiveTime = UnityEngine.Time.realtimeSinceStartup;

						return NetEventType.Nothing;
					}
				case EventType.Connect:
					{
                        channel = INVALID_CHANNEL;
						payload = new ArraySegment<byte>();
						receiveTime = UnityEngine.Time.realtimeSinceStartup;

						connectedEnetPeers.Add(@event.Peer.ID, @event.Peer);

						@event.Peer.PingInterval(PingInterval);
						@event.Peer.Timeout(TimeoutLimit, TimeoutMinimum, TimeoutMaximum);

						return NetEventType.Connect;
					}
				case EventType.Disconnect:
					{
                        channel = INVALID_CHANNEL;
						payload = new ArraySegment<byte>();
						receiveTime = UnityEngine.Time.realtimeSinceStartup;

						connectedEnetPeers.Remove(@event.Peer.ID);

						return NetEventType.Disconnect;
					}
				case EventType.Receive:
					{
                        channel = @event.ChannelID;
						receiveTime = UnityEngine.Time.realtimeSinceStartup;
						int size = @event.Packet.Length;

						if (size > messageBuffer.Length)
						{
							byte[] tempBuffer;

							if (temporaryBufferReference != null && temporaryBufferReference.IsAlive && ((byte[])temporaryBufferReference.Target).Length >= size)
							{
								tempBuffer = (byte[])temporaryBufferReference.Target;
							}
							else
							{
								tempBuffer = new byte[size];
								temporaryBufferReference = new WeakReference(tempBuffer);
							}

							@event.Packet.CopyTo(tempBuffer);
							payload = new ArraySegment<byte>(tempBuffer, 0, size);
						}
						else
						{
							@event.Packet.CopyTo(messageBuffer);
							payload = new ArraySegment<byte>(messageBuffer, 0, size);
						}

						@event.Packet.Dispose();

						return NetEventType.Data;
					}
				case EventType.Timeout:
					{
						channel = INVALID_CHANNEL;
						payload = new ArraySegment<byte>();
						receiveTime = UnityEngine.Time.realtimeSinceStartup;

						connectedEnetPeers.Remove(@event.Peer.ID);

						return NetEventType.Disconnect;
					}
				default:
					{
						channel = INVALID_CHANNEL;
						payload = new ArraySegment<byte>();
						receiveTime = UnityEngine.Time.realtimeSinceStartup;

						return NetEventType.Nothing;
					}
			}
		}

		public override void StartClient()
		{
			host = new Host();

            host.Create(1, channelCount);


			Address targetAddress = new Address();
            targetAddress.Port = (ushort)port;
            targetAddress.SetHost(address);

            Peer serverPeer = host.Connect(targetAddress, channelCount);

			serverPeer.PingInterval(PingInterval);
			serverPeer.Timeout(TimeoutLimit, TimeoutMinimum, TimeoutMaximum);

			serverPeerId = serverPeer.ID;
		}

		public override void StartServer()
		{
			host = new Host();

			Address targetAddress = new Address();
            targetAddress.Port = (ushort)port;

            host.Create(targetAddress, MaxClients, channelCount);

			//return SocketTask.Done.AsTasks();
		}

		public override void DisconnectRemoteClient(ulong clientID)
		{
			GetEnetConnectionDetails(serverPeerId, out uint peerId);

			connectedEnetPeers[peerId].DisconnectNow(0);
		}

		public override void DisconnectLocalClient()
		{
			host.Flush();

			GetEnetConnectionDetails(serverPeerId, out uint peerID);

			if (connectedEnetPeers.ContainsKey(peerID))
			{
				connectedEnetPeers[peerID].DisconnectNow(0);
			}
		}

		public override ulong GetCurrentRtt(ulong clientID)
		{
			GetEnetConnectionDetails(clientID, out uint peerID);

			return connectedEnetPeers[peerID].RoundTripTime;
		}

		public override void Shutdown()
		{
			if (host != null)
			{
				host.Flush();
				host.Dispose();
			}

			Library.Deinitialize();
		}

		public override void Init()
		{
			Library.Initialize();
			connectedEnetPeers.Clear();

			messageBuffer = new byte[MessageBufferSize];
		}

		private PacketFlags ChannelTypeToPacketFlags(ChannelType channelType)
		{
            switch(channelType)
            {
                case ChannelType.Unreliable:
                    {
                        return PacketFlags.Unsequenced;
                    }
                case ChannelType.Reliable:
                    {
                        // ENET Does not support ReliableUnsequenced.
                        // https://github.com/MidLevel/MLAPI.Transports/pull/5#issuecomment-498311723
                        return PacketFlags.Reliable;
                    }
                case ChannelType.ReliableSequenced:
                    {
                        return PacketFlags.Reliable;
                    }
                case ChannelType.ReliableFragmentedSequenced:
                    {
                        return PacketFlags.Reliable;
                    }
                case ChannelType.UnreliableSequenced:
                    {
                        return PacketFlags.None;
                    }
                default:
                    return PacketFlags.None;
            }
		}

        public override ChannelType GetUnsupportedChannelTypeFallback(ChannelType channelType)
        {
            if(channelType == ChannelType.Reliable)
                return ChannelType.ReliableSequenced;
            return channelType;
        }

        public ulong GetMLAPIClientID(uint peerID, bool isServer)
		{
			if (isServer)
			{
				return 0;
            }
            return peerID + 1;
        }

		public void GetEnetConnectionDetails(ulong clientID, out uint peerID)
		{
			if (clientID == 0)
			{
				peerID = serverPeerId;
			}
			else
			{
				peerID = (uint)clientID - 1;
			}
		}
	}
}