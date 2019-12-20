using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using MLAPI.Serialization.Pooled;
using BitStream = MLAPI.Serialization.BitStream;
using MLAPI.Hashing;

using Isaac.Network;
using Isaac.Network.Messaging;

namespace MLAPI.Messaging
{
    /// <summary>
    /// The manager class to manage custom messages.
    /// </summary>
    public static class CustomMessagingManager
    {
        
        #region Unnamed
        /// <summary>
        /// Delegate used for incoming unnamed messages
        /// </summary>
        /// <param name="clientID">The clientId that sent the message</param>
        /// <param name="stream">The stream containing the message data</param>
        public delegate void UnnamedMessageDelegate(ulong clientID, Stream stream);

        /// <summary>
        /// Event invoked when unnamed messages arrive
        /// </summary>
        public static event UnnamedMessageDelegate OnUnnamedMessage;

        internal static void InvokeUnnamedMessage(ulong clientID, Stream stream)
        {
            OnUnnamedMessage?.Invoke(clientID, stream);
        }


        /// <summary>
        /// Sends unnamed message to a list of clients
        /// </summary>
        /// <param name="clientIDs">The clients to send to, sends to everyone if null</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel to send the data on</param>
        public static void SendUnnamedMessage(List<ulong> clientIDs, BitStream stream, string channel = null)
        {
            if(!NetworkManager.Get().isServer)
            {
                Debug.LogWarning("Can not send unnamed messages to multiple users as a client");
                return;
            }

            if(clientIDs == null)
            {
                for(int i = 0; i < NetworkManager.Get().connectedClients.Count; i++)
                {
                    //InternalMessageSender.Send(NetworkManager.Get().connectedClients[i].clientID, MessageType.NETWORK_UNNAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream);
                }
            }
            else
            {
                for(int i = 0; i < clientIDs.Count; i++)
                {
                    //InternalMessageSender.Send(clientIDs[i], MessageType.NETWORK_UNNAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream);
                }
            }
        }

        /// <summary>
        /// Sends a unnamed message to a specific client
        /// </summary>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel tos end the data on</param>
        public static void SendUnnamedMessage(ulong clientId, BitStream stream, string channel = null)
        {
            //InternalMessageSender.Send(clientId, MessageType.NETWORK_UNNAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream);
        }
        #endregion
        #region Named

        /// <summary>
        /// Delegate used to handle named messages
        /// </summary>
        public delegate void HandleNamedMessageDelegate(ulong sender, Stream payload);

        private static readonly Dictionary<ulong, HandleNamedMessageDelegate> namedMessageHandlers = new Dictionary<ulong, HandleNamedMessageDelegate>();

        internal static void InvokeNamedMessage(ulong hash, ulong sender, Stream stream)
        {
            if(namedMessageHandlers.ContainsKey(hash))
            {
                namedMessageHandlers[hash](sender, stream);
            }
        }

        /// <summary>
        /// Registers a named message handler delegate.
        /// </summary>
        /// <param name="name">Name of the message.</param>
        /// <param name="callback">The callback to run when a named message is received.</param>
        public static void RegisterNamedMessageHandler(string name, HandleNamedMessageDelegate callback)
        {
            ulong hash = name.GetStableHash(NetworkManager.Get().config.rpcHashSize);

            namedMessageHandlers[hash] = callback;
        }

        /// <summary>
        /// Sends a named message
        /// </summary>
        /// <param name="name">The message name to send</param>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel tos end the data on</param>
        public static void SendNamedMessage(string name, ulong clientId, Stream stream, string channel = null)
        {
            ulong hash = name.GetStableHash(NetworkManager.Get().config.rpcHashSize);

            using(PooledBitStream messageStream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(messageStream))
                {
                    writer.WriteUInt64Packed(hash);
                }

                messageStream.CopyFrom(stream);

                //InternalMessageSender.Send(clientId, MessageType.NETWORK_NAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, messageStream);
            }
        }

        /// <summary>
        /// Sends the named message
        /// </summary>
        /// <param name="name">The message name to send</param>
        /// <param name="clientIds">The clients to send to, sends to everyone if null</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel to send the data on</param>
        public static void SendNamedMessage(string name, List<ulong> clientIds, Stream stream, string channel = null)
        {
            ulong hash = name.GetStableHash(NetworkManager.Get().config.rpcHashSize);

            using(PooledBitStream messageStream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(messageStream))
                {
                    writer.WriteUInt64Packed(hash);
                }

                messageStream.CopyFrom(stream);

                if(!NetworkManager.Get().isServer)
                {
                    Debug.LogWarning("Can not send named messages to multiple users as a client");
                    return;
                }
                if(clientIds == null)
                {
                    for(int i = 0; i < NetworkManager.Get().connectedClients.Count; i++)
                    {
                        //InternalMessageSender.Send(NetworkManager.Get().connectedClients[i].clientID, MessageType.NETWORK_NAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, messageStream);
                    }
                }
                else
                {
                    for(int i = 0; i < clientIds.Count; i++)
                    {
                        //InternalMessageSender.Send(clientIds[i], MessageType.NETWORK_NAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, messageStream);
                    }
                }
            }
        }
        #endregion
        
    }
}
