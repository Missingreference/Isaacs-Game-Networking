using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Elanetic.Network
{
	public abstract class NetworkTransport
	{
        public const byte DEFAULT_CHANNEL = 0;
        public const byte INVALID_CHANNEL = 255;

        public string address { get; set; } = "127.0.0.1";
        public int port { get; set; } = 7777;

        /// <summary>
        /// A constant clientId that represents the server.
        /// When this value is found in methods such as Send, it should be treated as a placeholder that means "the server"
        /// </summary>
        public abstract ulong serverID { get; }

        public abstract ChannelType supportedChannelTypes { get; }

        public int channelCount => m_ChannelCount;

        /// <summary>
        /// Delegate for transport events.
        /// </summary>
        public delegate void TransportEventDelegate(NetEventType type, ulong clientId, byte channel, ArraySegment<byte> payload, float receiveTime);

		/// <summary>
		/// Occurs when the transport has a new transport event. Can be used to make an event based transport instead of a poll based.
		/// Invokation has to occur on the Unity thread in the Update loop.
		/// </summary>
        /// Disable "event is not used" warning since its in fact being used in Network Manager and invoked in deriving transports
        #pragma warning disable CS0067
		public event TransportEventDelegate OnTransportEvent;
        #pragma warning restore CS0067

        private readonly List<TransportChannel> m_Channels = new List<TransportChannel>();
        private readonly Dictionary<string, byte> m_ChannelsByName = new Dictionary<string, byte>();
        private List<byte> m_FreeChannels = new List<byte>();
        private int m_ChannelCount = 0;


        public NetworkTransport()
        {
            //Register built-in channels
            //Using GetUnsupportedChannelTypeFallback to ensure that no warning pops up for users. Should probably not be used this way by users so that you can be sure of what channel type is being used in your code
            if((supportedChannelTypes & ChannelType.Reliable) == ChannelType.Reliable)
                RegisterChannel("NETWORK_DEFAULT", ChannelType.Reliable);
            else
                RegisterChannel("NETWORK_DEFAULT", GetUnsupportedChannelTypeFallback(ChannelType.Reliable));
        }

		/// <summary>
		/// Send a payload to the specified clientID, data and channelName.
		/// </summary>
		/// <param name="clientID">The clientID to send to</param>
		/// <param name="data">The data to send</param>
		/// <param name="channel">The channel to send data to</param>
		public abstract void Send(ulong clientID, ArraySegment<byte> data, byte channel);

        public void Send(ulong cliendID, ArraySegment<byte> data, string channelName) => Send(cliendID, data, GetChannelByName(channelName));

		/// <summary>
		/// Polls for incoming events, with an extra output parameter to report the precise time the event was received.
		/// </summary>
		/// <param name="clientID">The clientId this event is for</param>
		/// <param name="channel">The channel the data arrived at. This is usually used when responding to things like RPCs</param>
		/// <param name="payload">The incoming data payload</param>
		/// <param name="receiveTime">The time the event was received, as reported by Time.realtimeSinceStartup.</param>
		/// <returns>Returns the event type</returns>
		public abstract NetEventType PollEvent(out ulong clientID, out byte channel, out ArraySegment<byte> payload, out float receiveTime);

		/// <summary>
		/// Connects client to server
		/// </summary>
		public abstract void StartClient();

		/// <summary>
		/// Starts to listen for incoming clients.
		/// </summary>
		public abstract void StartServer();

		/// <summary>
		/// Disconnects a client from the server
		/// </summary>
		/// <param name="clientID">The clientID to disconnect</param>
		public abstract void DisconnectRemoteClient(ulong clientID);

		/// <summary>
		/// Disconnects the local client from the server
		/// </summary>
		public abstract void DisconnectLocalClient();

		/// <summary>
		/// Gets the round trip time for a specific client. This method is optional
		/// </summary>
		/// <param name="clientID">The clientID to get the rtt from</param>
		/// <returns>Returns the round trip time in milliseconds </returns>
		public abstract ulong GetCurrentRtt(ulong clientID);

		/// <summary>
		/// Shuts down the transport
		/// </summary>
		public abstract void Shutdown();

		/// <summary>
		/// Initializes the transport
		/// </summary>
		public abstract void Init();

        #region Channel Management

        /// <summary>
        /// Channels are used in multiple cases such as when stalling(when sending messages) occurs on network transports the stalling is isolated to a specific channel and won't affect other areas of network messaging to maximize performance.
        /// Be aware registering and unregistering channels while the network is running may not take effect until the transport has been shutdown and reinitialized.
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="channelType"></param>
        /// <returns></returns>
        public byte RegisterChannel(string channelName, ChannelType channelType)
        {
            //Check if channeName is valid
            if(string.IsNullOrWhiteSpace(channelName))
            {
                Debug.LogError("Paramater channelName is null or whitespace.");
                return INVALID_CHANNEL; //Return invalid channel
            }

            //Check if the channel name is taken
            if(m_ChannelsByName.ContainsKey(channelName))
            {
                Debug.LogError("Channel name '" + channelName + "' is already registered.");
                return INVALID_CHANNEL; //Return invalid channel
            }

            //Check if there are channels left
            byte chosenChannelIndex;

            if(m_FreeChannels.Count > 0)
            {
                chosenChannelIndex = m_FreeChannels[0];
                m_FreeChannels.RemoveAt(0);
                m_Channels[chosenChannelIndex] = new TransportChannel() { name = channelName, channel = chosenChannelIndex, channelType = channelType };
            }
            else if(m_Channels.Count == byte.MaxValue)
            {
                //Remember 0 is used for NETWORK_DEFAULT, 255 is invalid and whatever built-in channels being used by the Network Manager.
                Debug.LogError("No channels left to register. A maximum of 255 channels can be registered.");
                return INVALID_CHANNEL; //Return invalid channel
            }
            else
            {
                chosenChannelIndex = (byte)m_Channels.Count;
                m_Channels.Add(new TransportChannel() { name = channelName, channel = chosenChannelIndex, channelType = channelType });
            }

            //Warn about unsupported channel type
            if((supportedChannelTypes & channelType) != channelType)
            {
                Debug.LogWarning("Channel Type '" + channelType + "' is not supported by this transport(" + this.GetType() + ") and will use an alternative channel type internally when a message with this channel type is used.");
            }

            //Do register
            m_ChannelsByName.Add(channelName, chosenChannelIndex);
            m_ChannelCount++;
            return chosenChannelIndex;
        }

        /// <summary>
        /// 
        /// Be aware registering and unregistering channels while the network is running may not take effect until the transport has been shutdown and reinitialized.
        /// </summary>
        /// <param name="channel"></param>
        public void UnregisterChannel(byte channel)
        {
            //Check if channel is valid and registered
            if(channel >= m_Channels.Count || channel == INVALID_CHANNEL || m_Channels[channel] == null)
            {
                throw new ArgumentException("Channel '" + channel + "' is already not registered or invalid.", nameof(channel));
            }

            //Disallow unregistering of built-in channels
            if(channel == DEFAULT_CHANNEL)
            {
                throw new ArgumentException("Channel '" + channel + "' is a built-in channel and cannot be unregistered.", nameof(channel));
            }


            //Do unregisteer
            m_ChannelCount--;
            m_ChannelsByName.Remove(GetChannelName(channel));
            m_Channels[channel] = null;
            m_FreeChannels.Add(channel);
        }

        /// <summary>
        /// Gets the byte channel index by the name of its channel.
        /// Failing will print an error message and return the invalid channel(255).
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public byte GetChannelByName(string channelName)
        {
            if(m_ChannelsByName.TryGetValue(channelName, out byte channel))
            {
                return channel;
            }
            throw new ArgumentException("Channel name '" + channelName + "' is not a valid registered channel.", nameof(channelName));
        }

        public string GetChannelName(byte channel)
        {
            if(channel >= m_Channels.Count || channel == INVALID_CHANNEL)
            {
                throw new ArgumentException("Could not get channel name. Channel '" + channel + "' is not registered or invalid.", nameof(channel));
            }
            TransportChannel transportChannel = m_Channels[channel];
            if(transportChannel == null)
            {
                throw new ArgumentException("Could not get channel name. Channel '" + channel + "' is not registered or invalid.", nameof(channel));
            }
            return transportChannel.name;
        }

        public ChannelType GetChannelType(byte channel)
        {
            if(channel >= m_Channels.Count || channel == INVALID_CHANNEL)
            {
                Debug.Log("a1");
                throw new ArgumentException("Could not get channel type. Channel '" + channel + "' is not registered or invalid.", nameof(channel));
            }

            TransportChannel transportChannel = m_Channels[channel];
            if(transportChannel == null)
            {
                Debug.Log("b2");
                throw new ArgumentException("Could not get channel type. Channel '" + channel + "' is not registered or invalid.", nameof(channel));
            }

            return transportChannel.channelType;
        }

        public bool TryGetTransportChannel(byte channel, out TransportChannel transportChannel)
        {
            if(channel >= m_Channels.Count)
            {
                transportChannel = null;
                return false;
            }
            transportChannel = m_Channels[channel];
            return transportChannel != null; //Null comparison is probably faster than doing a m_FreeChannels.Contains call
        }

        public bool TryGetTransportChannel(string channelName, out TransportChannel transportChannel)
        {
            if(m_ChannelsByName.TryGetValue(channelName, out byte channel))
            {
                transportChannel = m_Channels[channel];
                return true;
            }

            transportChannel = null;
            return false;
        }

        public virtual ChannelType GetUnsupportedChannelTypeFallback(ChannelType channelType) { return channelType; }

        #endregion

    } //Class

    [Flags]
	public enum ChannelType
	{
		/// <summary>
		/// Unreliable message
		/// </summary>
		Unreliable = 1,
		/// <summary>
		/// Unreliable with sequencing
		/// </summary>
		UnreliableSequenced = 2,
		/// <summary>
		/// Reliable message
		/// </summary>
		Reliable = 4,
		/// <summary>
		/// Reliable message where messages are guaranteed to be in the right order
		/// </summary>
		ReliableSequenced = 8,
		/// <summary>
		/// A reliable message with guaranteed order with fragmentation support
		/// </summary>
		ReliableFragmentedSequenced = 16
	}

	/// <summary>
	/// Represents a netEvent when polling
	/// </summary>
	public enum NetEventType
	{
		/// <summary>
		/// New data is received
		/// </summary>
		Data,
		/// <summary>
		/// A client is connected, or client connected to server
		/// </summary>
		Connect,
		/// <summary>
		/// A client disconnected, or client disconnected from server
		/// </summary>
		Disconnect,
		/// <summary>
		/// No new event
		/// </summary>
		Nothing
	}

	[Serializable]
	public class TransportChannel
	{
		/// <summary>
		/// The name of the channel
		/// </summary>
		public string name;

        /// <summary>
        /// The actual byte channel
        /// </summary>
        public byte channel;

		/// <summary>
		/// The type of channel
		/// </summary>
		public ChannelType channelType;
	} //Class
} //Namespace
