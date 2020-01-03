using System;
using System.Collections.Generic;

using UnityEngine;

using MLAPI.Internal;
using MLAPI.Security;
using BitStream = MLAPI.Serialization.BitStream;

namespace Isaac.Network.Messaging
{
    static class MessageSender
    {
        static public void Send(ulong clientID, MessageType messageType, BitStream messageStream)
        {
            Send(clientID, (byte)messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }
        static public void Send(ulong clientID, byte messageType, BitStream messageStream)
        {
            Send(clientID, messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void Send(ulong clientID, MessageType messageType, string channelName, BitStream messageStream)
        {
            Send(clientID, (byte)messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void Send(ulong clientID, byte messageType, string channelName, BitStream messageStream)
        {
            Send(clientID, messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void Send(ulong clientID, MessageType messageType, byte channel, BitStream messageStream)
        {
            Send(clientID, (byte)messageType, channel, messageStream);
        }

        static public void Send(ulong clientID, byte messageType, byte channel, BitStream messageStream)
        {
            messageStream.PadStream();

            if(NetworkManager.Get().isServer && clientID == NetworkManager.Get().serverID)
                return;

            using(BitStream stream = MessagePacker.WrapMessage(messageType, clientID, messageStream, SecuritySendFlags.None))
            {
                NetworkManager.Get().transport.Send(clientID, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), channel);
            }
        }


        static public void SendToSpecific(List<ulong> clientIDs, MessageType messageType, BitStream messageStream)
        {
            SendToSpecific(clientIDs, (byte)messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void SendToSpecific(List<ulong> clientIDs, byte messageType, BitStream messageStream)
        {
            SendToSpecific(clientIDs, messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void SendToSpecific(List<ulong> clientIDs, MessageType messageType, string channelName, BitStream messageStream)
        {
            SendToSpecific(clientIDs, (byte)messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void SendToSpecific(List<ulong> clientIDs, byte messageType, string channelName, BitStream messageStream)
        {
            SendToSpecific(clientIDs, messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void SendToSpecific(List<ulong> clientIDs, MessageType messageType, byte channel, BitStream messageStream)
        {
            SendToSpecific(clientIDs, (byte)messageType, channel, messageStream);
        }

        //Slightly slower because of all the safety checks with the list but convenient
        static public void SendToSpecific(List<ulong> clientIDs, byte messageType, byte channel, BitStream messageStream)
        {
            if(clientIDs == null)
            {
                Debug.LogError("Client ID list is null.");
                return;
            }
            if(clientIDs.Count == 0) return; //No one to send to.

            messageStream.PadStream();

            using(BitStream stream = MessagePacker.WrapMessage(messageType, 0, messageStream, SecuritySendFlags.None))
            {
                for(int i = 0; i < clientIDs.Count; i++)
                {
                    if(NetworkManager.Get().isServer && clientIDs[i] == NetworkManager.Get().serverID)
                        continue;

                    NetworkManager.Get().transport.Send(clientIDs[i], new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), channel);
                }
            }
        }


        static public void SendToAll(MessageType messageType, BitStream messageStream)
        {
            SendToAll((byte)messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void SendToAll(byte messageType, BitStream messageStream)
        {
            SendToAll(messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void SendToAll(MessageType messageType, string channelName, BitStream messageStream)
        {
            SendToAll((byte)messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void SendToAll(byte messageType, string channelName, BitStream messageStream)
        {
            SendToAll(messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void SendToAll(MessageType messageType, byte channel, BitStream messageStream)
        {
            SendToAll((byte)messageType, channel, messageStream);
        }

        static public void SendToAll(byte messageType, byte channel, BitStream messageStream)
        {
            messageStream.PadStream();

            using(BitStream stream = MessagePacker.WrapMessage(messageType, 0, messageStream, SecuritySendFlags.None))
            {
                using(List<ulong>.Enumerator clients = NetworkManager.Get().clients)
                {
                    while(clients.MoveNext())
                    {
                        if(NetworkManager.Get().isServer && clients.Current == NetworkManager.Get().serverID)
                            continue;

                        NetworkManager.Get().transport.Send(clients.Current, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), channel);
                    }
                }
            }
        }


        static public void SendToAllExcept(ulong clientIDToIgnore, MessageType messageType, BitStream messageStream)
        {
            SendToAllExcept(clientIDToIgnore, (byte)messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void SendToAllExcept(ulong clientIDToIgnore, byte messageType, BitStream messageStream)
        {
            SendToAllExcept(clientIDToIgnore, messageType, NetworkTransport.DEFAULT_CHANNEL, messageStream);
        }

        static public void SendToAllExcept(ulong clientIDToIgnore, MessageType messageType, string channelName, BitStream messageStream)
        {
            SendToAllExcept(clientIDToIgnore, (byte)messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void SendToAllExcept(ulong clientIDToIgnore, byte messageType, string channelName, BitStream messageStream)
        {
            SendToAllExcept(clientIDToIgnore, messageType, NetworkManager.Get().transport.GetChannelByName(channelName), messageStream);
        }

        static public void SendToAllExcept(ulong clientIDToIgnore, MessageType messageType, byte channel, BitStream messageStream)
        {
            SendToAllExcept(clientIDToIgnore, (byte)messageType, channel, messageStream);
        }

        static public void SendToAllExcept(ulong clientIDToIgnore, byte messageType, byte channel, BitStream messageStream)
        {
            messageStream.PadStream();

            using(BitStream stream = MessagePacker.WrapMessage(messageType, 0, messageStream, SecuritySendFlags.None))
            {
                using(List<ulong>.Enumerator clients = NetworkManager.Get().clients)
                {
                    while(clients.MoveNext())
                    {
                        if(clients.Current == clientIDToIgnore ||
                            (NetworkManager.Get().isServer && clients.Current == NetworkManager.Get().serverID))
                            continue;

                        NetworkManager.Get().transport.Send(clients.Current, new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), channel);
                    }
                }
            }
        }
    }
}