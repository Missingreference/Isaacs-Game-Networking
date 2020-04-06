using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

using UnityEngine;
using UnityEngine.Serialization;

using MLAPI.Serialization.Pooled;
using BitStream = MLAPI.Serialization.BitStream;
using MLAPI.Hashing;

using Elanetic.Network.Messaging;

namespace Elanetic.Network
{
	public class NetworkConfig
    {
        /// <summary>
        // Change this to break compability between compiled network changes and updates. This will prevent clients from connecting to a server with a protocal mismatch between the server and client.
        // For example you come out with a new message type in the network system. That is gameplay breaking since the older client won't know what the new
        // message type is and this protocal version should be changed so that clients cannot connect to eachother and have unknown issues.
        /// </summary>
		public string protocalVersion = "1.0";
		/// <summary>
		/// Amount of times per second the receive queue is emptied and all messages inside are processed.
		/// </summary>
		public int receiveTickrate = 64;
		/// <summary>
		/// The max amount of messages to process per ReceiveTickrate. This is to prevent flooding.
		/// </summary>
		public int maxReceiveEventsPerTickRate = 500;
		/// <summary>
		/// The amount of times per second internal frame events will occur, examples include SyncedVar send checking.
		/// </summary>
		public int eventTickrate = 64;
		/// <summary>
		/// The maximum amount of NetworkedObject's to process per tick.
		/// This is useful to prevent the MLAPI from hanging a frame
		/// Set this to less than or equal to 0 for unlimited
		/// </summary>
		public int maxObjectUpdatesPerTick = -1;
		/// <summary>
		/// The amount of seconds to wait for handshake to complete before timing out a client
		/// </summary>
		public int clientConnectionBufferTimeout = 10;
		/// <summary>
		/// The data to send during connection which can be used to decide on if a client should get accepted
		/// </summary>
		public byte[] connectionData = new byte[0];
		/// <summary>
		/// The amount of seconds to keep a lag compensation position history
		/// </summary>
		public int secondsHistory = 5;
		/// <summary>
		/// If your logic uses the NetworkedTime, this should probably be turned off. If however it's needed to maximize accuracy, this is recommended to be turned on
		/// </summary>
		public bool enableTimeResync = false;
		/// <summary>
		/// If time resync is turned on, this specifies the interval between syncs in seconds.
		/// </summary>
		public int timeResyncInterval = 30;
		/// <summary>
		/// Whether or not to enable the NetworkedVar system. This system runs in the Update loop and will degrade performance, but it can be a huge convenience.
		/// Only turn it off if you have no need for the NetworkedVar system.
		/// </summary>
		public bool enableNetworkedVar = true;
		/// <summary>
		/// Whether or not to ensure that NetworkedVars can be read even if a client accidentally writes where its not allowed to. This costs some CPU and bandwdith.
		/// </summary>
		public bool ensureNetworkedVarLengthSafety = false;
		/// <summary>
		/// If true, NetworkIds will be reused after the NetworkIdRecycleDelay.
		/// </summary>
		public bool recycleNetworkIDs = true;
		/// <summary>
		/// The amount of seconds a NetworkId has to be unused in order for it to be reused.
		/// </summary>
		public float networkIDRecycleDelay = 120f;
		/// <summary>
		/// Decides how many bytes to use for Rpc messaging. Leave this to 2 bytes unless you are facing hash collisions
		/// </summary>
		public HashMode rpcHashSize = HashMode.Hash16;

        /// <summary>
        /// Whether or not the client to send a network message to the server whenever a client loads or unloads a scene.
        /// </summary>
        public bool clientSendSceneEvents = true;
        /// <summary>
        /// Whether or not the server to send a network message to all clients whenever the server loads or unloads a scene.
        /// </summary>
        public bool serverSendSceneEvents = true;

		/// <summary>
		/// Returns a base64 encoded version of the config
		/// </summary>
		/// <returns></returns>
		public string ToBase64()
		{
			NetworkConfig config = this;
			using (PooledBitStream stream = PooledBitStream.Get())
			{
				using (PooledBitWriter writer = PooledBitWriter.Get(stream))
				{
					writer.WriteString(config.protocalVersion);

					writer.WriteInt32Packed(config.receiveTickrate);
					writer.WriteInt32Packed(config.maxReceiveEventsPerTickRate);
					writer.WriteInt32Packed(config.eventTickrate);
					writer.WriteInt32Packed(config.clientConnectionBufferTimeout);
					writer.WriteInt32Packed(config.secondsHistory);
					writer.WriteBool(config.enableTimeResync);
					writer.WriteBool(config.ensureNetworkedVarLengthSafety);
					writer.WriteBits((byte)config.rpcHashSize, 3);
					writer.WriteBool(recycleNetworkIDs);
					writer.WriteSinglePacked(networkIDRecycleDelay);
					writer.WriteBool(enableNetworkedVar);
                    writer.WriteBool(clientSendSceneEvents);
                    writer.WriteBool(serverSendSceneEvents);
                    stream.PadStream();

					return Convert.ToBase64String(stream.ToArray());
				}
			}
		}

		/// <summary>
		/// Sets the NetworkConfig data with that from a base64 encoded version
		/// </summary>
		/// <param name="base64">The base64 encoded version</param>
		public void FromBase64(string base64)
		{
			NetworkConfig config = this;
			byte[] binary = Convert.FromBase64String(base64);
			using (BitStream stream = new BitStream(binary))
			{
				using (PooledBitReader reader = PooledBitReader.Get(stream))
				{
					config.protocalVersion = reader.ReadString().ToString();

					config.receiveTickrate = reader.ReadInt32Packed();
					config.maxReceiveEventsPerTickRate = reader.ReadInt32Packed();
					config.eventTickrate = reader.ReadInt32Packed();
					config.clientConnectionBufferTimeout = reader.ReadInt32Packed();
					config.secondsHistory = reader.ReadInt32Packed();
					config.enableTimeResync = reader.ReadBool();
					config.ensureNetworkedVarLengthSafety = reader.ReadBool();
					config.rpcHashSize = (HashMode)reader.ReadBits(3);
					config.recycleNetworkIDs = reader.ReadBool();
					config.networkIDRecycleDelay = reader.ReadSinglePacked();
					config.enableNetworkedVar = reader.ReadBool();
                    config.clientSendSceneEvents = reader.ReadBool();
                    config.serverSendSceneEvents = reader.ReadBool();
                }
			}
		}


		private ulong? ConfigHash = null;
		/// <summary>
		/// Gets a SHA256 hash of parts of the NetworkingConfiguration instance
		/// </summary>
		/// <param name="cache"></param>
		/// <returns></returns>
		public ulong GetConfig(bool cache = true)
		{
			if (ConfigHash != null && cache)
				return ConfigHash.Value;

			using (PooledBitStream stream = PooledBitStream.Get())
			{
				using (PooledBitWriter writer = PooledBitWriter.Get(stream))
				{
					writer.WriteString(protocalVersion);

					writer.WriteBool(enableNetworkedVar);
					writer.WriteBool(ensureNetworkedVarLengthSafety);
					writer.WriteBits((byte)rpcHashSize, 3);
					stream.PadStream();

					if (cache)
					{
						ConfigHash = stream.ToArray().GetStableHash64();
						return ConfigHash.Value;
					}

					return stream.ToArray().GetStableHash64();
				}
			}
		}

		/// <summary>
		/// Compares a SHA256 hash with the current NetworkingConfiguration instances hash
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		public bool CompareConfig(ulong hash)
		{
			return hash == GetConfig();
		}

	} //Class
} //Namespace