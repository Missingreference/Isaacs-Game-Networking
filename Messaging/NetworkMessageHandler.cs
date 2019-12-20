﻿using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using MLAPI.Security;
using BitStream = MLAPI.Serialization.BitStream;
using MLAPI.Spawning;
using MLAPI.Messaging;

using Isaac.Network.Connection;
using Isaac.Network.Spawning;

namespace Isaac.Network.Messaging
{
    public class NetworkMessageHandler : NetworkModule
    {

        public enum NetworkMessageReceiver
        {
            Client,
            Server,
            Both
        }

        public delegate void MessageCallbackDelagate(ulong sendingClientID, Stream stream, float receiveTime);

        /// <summary>
        /// The number of built-in message types including 0(INVALID). Does not include any messages not in the MessageType enum.
        /// </summary>
        public int builtInMessageTypeCount
        {
            get
            {
                if(m_BuiltInMessageTypeCount == null)
                    m_BuiltInMessageTypeCount = Enum.GetValues(typeof(MessageType)).Length;
                return m_BuiltInMessageTypeCount.Value;
            }
        }

        private readonly string[] m_MessageNames = new string[255];
        private readonly MessageCallbackDelagate[] m_Callbacks = new MessageCallbackDelagate[255];
        private readonly NetworkMessageReceiver[] m_Receivers = new NetworkMessageReceiver[255];

        private byte m_FirstFreeIndex = 0;

        private int? m_BuiltInMessageTypeCount = null;

        public NetworkMessageHandler()
        {
            //RegisterBuiltInMessages();
        }

        public override void Init(NetworkModule[] loadedDependencies)
        {

        }

        /// <summary>
        /// Add a custom message to be handled. Also registered messages CANNOT be unregistered. Maximum of 256(size of byte) messages can be registered (including 0(INVALID) and all built-in messages).
        /// If you want more than that use MLAPI's CustomMessageManager. It uses HashSets instead of an array so its slower by very little and supports very slow but convenient unnamed messages. TODO: Update CustomMessageManager to latest version to support unregistering custom messages.
        /// Messages should be registered in the same order on the server and each client otherwise there will be game breaking issues occuring.
        /// </summary>
        /// <param name="messageName">The name of the message type.</param>
        /// <param name="callback">The callback when the message type has been received and handle the message type.</param>
        /// <param name="receiver">Whether the message type will be sent to only the client, server or both.</param>
        /// <returns>The byte index associated with the message type.</returns>
        public byte RegisterMessageType(string messageName, MessageCallbackDelagate callback, NetworkMessageReceiver receiver=NetworkMessageReceiver.Both)
        {
            //This is in place to prevent bad design. Networking messages should be setup before starting the server or the client.
            if(NetworkManager.Get().isRunning)
            {
                Debug.LogError("Cannot register message type while the Network Manager is running.");
                return (byte)MessageType.INVALID;
            }

            if(string.IsNullOrEmpty(messageName))
            {
                Debug.LogError("Parameter 'messageName' cannot be a null or empty string.");
                return (byte)MessageType.INVALID;
            }

            if(callback == null)
            {
                Debug.LogError("Parameter 'callback' cannot be null.");
                return (byte)MessageType.INVALID;
            }

            if(m_FirstFreeIndex == (byte)MessageType.INVALID) //This assumes INVALID is 255.
            {
                Debug.LogError("No more slots to register the message type. Use Custom Message Manager instead.");
                return (byte)MessageType.INVALID;
            }

            m_MessageNames[m_FirstFreeIndex] = messageName;
            m_Callbacks[m_FirstFreeIndex] = callback;
            m_Receivers[m_FirstFreeIndex] = receiver;

            m_FirstFreeIndex++;

            return (byte)(m_FirstFreeIndex-1);
        }

        public string GetMessageName(byte messageType)
        {
            if(messageType >= m_FirstFreeIndex && messageType != (byte)MessageType.INVALID)
            {
                return string.Empty;
            }

            return m_MessageNames[messageType];
        }

        /// <summary>
        /// Handle the valid messages received from NetworkManager.HandleIncomingData().
        /// </summary>
        /// <param name="sendingClientID"></param>
        /// <param name="messageType"></param>
        /// <param name="messageStream"></param>
        /// <param name="receiveTime"></param>
        public void HandleMessage(ulong sendingClientID, byte messageType, BitStream messageStream, float receiveTime)
        {
            if(messageType >= m_FirstFreeIndex)
            {
                if(messageType == (byte)MessageType.INVALID)
                {
                    Debug.LogWarning("Received invalid message.");
                    return;
                }

                Debug.LogError("Received unknown message type. Message type: " + messageType.ToString());
                return;
            }

            NetworkMessageReceiver receiver = m_Receivers[messageType];

            switch(receiver)
            {
                case NetworkMessageReceiver.Both:
                    m_Callbacks[messageType].Invoke(sendingClientID, messageStream, receiveTime);
                    break;
                case NetworkMessageReceiver.Client:
                    if(networkManager.isClient) m_Callbacks[messageType].Invoke(sendingClientID, messageStream, receiveTime);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if(!networkManager.isClient)
                        Debug.LogError("Client '" + sendingClientID + "' sent message type " + GetMessageName(messageType) + "(" + messageType.ToString() + ") when the Network Message Receiver for this message type is set to Client.");
#endif
                    break;
                case NetworkMessageReceiver.Server:
                    if(networkManager.isServer) m_Callbacks[messageType].Invoke(sendingClientID, messageStream, receiveTime);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if(!networkManager.isServer)
                        Debug.LogError("Server sent message type '" + GetMessageName(messageType) + "(" + messageType.ToString() + ")' when the Network Message Receiver for this message type is set to Server.");
#endif
                    break;
            }
            /*
            //Server only
            if(m_NetworkManager.isServer)
            {
                switch(messageType)
                {
                    case NetworkMessageType.NETWORK_CONNECTION_REQUEST:
                        HandleConnectionRequest(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_SERVER_RPC:
                        HandleServerRPC(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_SERVER_RPC_REQUEST:
                        HandleServerRPCRequest(sendingClientID, messageStream, channelName, security);
                        return;
                    case NetworkMessageType.NETWORK_CLIENT_RPC_RESPONSE:
                        HandleClientRPCResponse(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_CLIENT_SCENE_CHANGE_COMPLETE:
                        HandleClientSceneChange(sendingClientID, messageStream);
                        return;
                }
            }
            //Client only
            if(m_NetworkManager.isClient)
            {
                switch(messageType)
                {
                    case NetworkMessageType.NETWORK_CONNECTION_APPROVED:
                        HandleConnectionApproved(sendingClientID, messageStream, receiveTime);
                        return;
                    case NetworkMessageType.NETWORK_ADD_OBJECT:
                        HandleAddObject(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_DESTROY_OBJECT:
                        HandleDestroyObject(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_SCENE_CHANGE:

                        return;
                    case NetworkMessageType.NETWORK_CHANGE_OWNER:
                        HandleChangeOwner(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_ADD_OBJECTS:
                        HandleAddObjects(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_DESTROY_OBJECTS:
                        HandleDestroyObjects(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_TIME_SYNC:
                        HandleTimeSync(sendingClientID, messageStream, receiveTime);
                        return;
                    case NetworkMessageType.NETWORK_SERVER_RPC_RESPONSE:
                        HandleServerRPCResponse(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_CLIENT_RPC:
                        HandleClientRPC(sendingClientID, messageStream);
                        return;
                    case NetworkMessageType.NETWORK_CLIENT_RPC_REQUEST:
                        HandleClientRPCRequest(sendingClientID, messageStream, channelName, security);
                        return;
                }
            }
            //Applies to both server and client
            switch(messageType)
            {
                case NetworkMessageType.NETWORK_NETWORKED_VAR_DELTA:
                    HandleNetworkedVarDelta(sendingClientID, messageStream);
                    return;
                case NetworkMessageType.NETWORK_NETWORKED_VAR_UPDATE:
                    HandleNetworkedVarUpdate(sendingClientID, messageStream);
                    return;
                case NetworkMessageType.NETWORK_UNNAMED_MESSAGE:
                    HandleUnnamedMessage(sendingClientID, messageStream);
                    return;
                case NetworkMessageType.NETWORK_NAMED_MESSAGE:
                    HandleNamedMessage(sendingClientID, messageStream);
                    return;
                default:
                    Debug.LogError("Read unrecognized messageType " + messageType);
                    return;
            }
            */
        }

        /*
        private void HandleAddObject(ulong clientID, Stream stream)
        {
            //Read from server function NetworkBehaviourManager.SpawnOnNetwork()
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                ulong ownerID = reader.ReadUInt64Packed();
                Type behaviourType = (Type)reader.ReadObjectPacked(typeof(Type));

                Vector3? pos = null;
                Quaternion? rot = null;
                Vector3? scale = null;
                if(reader.ReadBool())
                {
                    pos = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                    rot = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                    scale = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                }


                NetworkBehaviour newBehaviour = m_BehaviourManager.CreateLocalNetworkObject();
                m_BehaviourManager.SetupLocalNetworkBehaviour(newBehaviour, networkID, ownerID, stream, false, 0, false);
                //NetworkBehaviour netObject = SpawnManager.CreateLocalNetworkedObject(softSync, instanceId, prefabHash, parentNetworkId, pos, rot);
                //SpawnManager.SpawnNetworkedObjectLocally(netObject, networkId, softSync, isPlayerObject, ownerId, stream, hasPayload, payLoadLength, true, false);
            }
        }

        private void HandleDestroyObject(ulong clientID, Stream stream)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                SpawnManager.OnDestroyObject(networkId, true);
            }
        }

        private void HandleChangeOwner(ulong clientID, Stream stream)
        {
            Debug.LogWarning("Using unimplemented function.");*/
        /*
        using (PooledBitReader reader = PooledBitReader.Get(stream))
        {
            ulong networkId = reader.ReadUInt64Packed();
            ulong ownerClientId = reader.ReadUInt64Packed();

            if (SpawnManager.SpawnedObjects[networkId].ownerClientID == NetworkManager.Get().clientID)
            {
                //We are current owner.
                SpawnManager.SpawnedObjects[networkId].InvokeBehaviourOnLostOwnership();
            }
            if (ownerClientId == NetworkManager.Get().clientID)
            {
                //We are new owner.
                SpawnManager.SpawnedObjects[networkId].InvokeBehaviourOnGainedOwnership();
            }
            SpawnManager.SpawnedObjects[networkId].ownerClientID = ownerClientId;

        }*//*
    }

    private void HandleAddObjects(ulong clientID, Stream stream)
    {
        using(PooledBitReader reader = PooledBitReader.Get(stream))
        {
            ushort objectCount = reader.ReadUInt16Packed();

            for(int i = 0; i < objectCount; i++)
            {
                HandleAddObject(clientID, stream);
            }
        }
    }

    private void HandleDestroyObjects(ulong clientID, Stream stream)
    {
        using(PooledBitReader reader = PooledBitReader.Get(stream))
        {
            ushort objectCount = reader.ReadUInt16Packed();

            for(int i = 0; i < objectCount; i++)
            {
                HandleDestroyObject(clientID, stream);
            }
        }
    }

    private void HandleNetworkedVarDelta(ulong clientID, Stream stream)
    {
        Debug.LogWarning("Using unimplemented function.");*/
           /*
           if (!NetworkManager.Get().config.EnableNetworkedVar)
           {
               Debug.LogWarning("NetworkedVar delta received but EnableNetworkedVar is false");
               return;
           }

           using (PooledBitReader reader = PooledBitReader.Get(stream))
           {
               ulong networkId = reader.ReadUInt64Packed();
               ushort orderIndex = reader.ReadUInt16Packed();

               if (SpawnManager.SpawnedObjects.ContainsKey(networkId))
               {
                   NetworkBehaviour instance = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(orderIndex);
                   if (instance == null)
                   {
                       Debug.LogWarning("NetworkedVar message recieved for a non existant behaviour");
                       return;
                   }
                   NetworkBehaviour.HandleNetworkedVarDeltas(instance.networkedVarFields, stream, clientId, instance);
               }
               else
               {
                   Debug.LogWarning("NetworkedVar message recieved for a non existant object with id: " + networkId);
                   return;
               }
           }
           *//*
       }

       private void HandleNetworkedVarUpdate(ulong clientID, Stream stream)
       {

           Debug.LogWarning("Using unimplemented function.");*//*
           if (!NetworkManager.Get().config.EnableNetworkedVar)
           {
               Debug.LogWarning("NetworkedVar update received but EnableNetworkedVar is false");
               return;
           }

           using (PooledBitReader reader = PooledBitReader.Get(stream))
           {
               ulong networkId = reader.ReadUInt64Packed();
               ushort orderIndex = reader.ReadUInt16Packed();

               if (SpawnManager.SpawnedObjects.ContainsKey(networkId))
               {
                   NetworkBehaviour instance = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(orderIndex);
                   if (instance == null)
                   {
                       Debug.LogWarning("NetworkedVar message recieved for a non existant behaviour");
                       return;
                   }
                   NetworkBehaviour.HandleNetworkedVarUpdate(instance.networkedVarFields, stream, clientId, instance);
               }
               else
               {
                   Debug.LogWarning("NetworkedVar message recieved for a non existant object with id: " + networkId);
                   return;
               }
           }*//*
       }

       private void HandleServerRPC(ulong clientID, Stream stream)
       {
           Debug.LogWarning("Use of unimplemented function.");*/
              /*
              using (PooledBitReader reader = PooledBitReader.Get(stream))
              {
                  ulong networkId = reader.ReadUInt64Packed();
                  ushort behaviourId = reader.ReadUInt16Packed();
                  ulong hash = reader.ReadUInt64Packed();

                  if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                  {
                      NetworkBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                      if (behaviour != null)
                      {
                          behaviour.OnRemoteServerRPC(hash, clientId, stream);
                      }
                  }
              }*//*
          }

          private void HandleServerRPCRequest(ulong clientID, Stream stream, string channelName, SecuritySendFlags security)
          {
              Debug.LogWarning("Use of unimplemented function.");*/
                 /*
                 using (PooledBitReader reader = PooledBitReader.Get(stream))
                 {
                     ulong networkId = reader.ReadUInt64Packed();
                     ushort behaviourId = reader.ReadUInt16Packed();
                     ulong hash = reader.ReadUInt64Packed();
                     ulong responseId = reader.ReadUInt64Packed();

                     if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                     {
                         NetworkBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                         if (behaviour != null)
                         {
                             object result = behaviour.OnRemoteServerRPC(hash, clientId, stream);

                             using (PooledBitStream responseStream = PooledBitStream.Get())
                             {
                                 using (PooledBitWriter responseWriter = PooledBitWriter.Get(responseStream))
                                 {
                                     responseWriter.WriteUInt64Packed(responseId);
                                     responseWriter.WriteObjectPacked(result);
                                 }

                                 InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_SERVER_RPC_RESPONSE, channelName, responseStream, security, SpawnManager.SpawnedObjects[networkId]);
                             }
                         }
                     }
                 }*//*
             }

             private void HandleServerRPCResponse(ulong clientID, Stream stream)
             {
                 using(PooledBitReader reader = PooledBitReader.Get(stream))
                 {
                     ulong responseId = reader.ReadUInt64Packed();

                     if(ResponseMessageManager.ContainsKey(responseId))
                     {
                         RpcResponseBase responseBase = ResponseMessageManager.GetByKey(responseId);

                         ResponseMessageManager.Remove(responseId);

                         responseBase.IsDone = true;
                         responseBase.Result = reader.ReadObjectPacked(responseBase.Type);
                         responseBase.IsSuccessful = true;
                     }
                 }
             }*/
                    /*
                    private void HandleClientRPC(ulong clientID, Stream stream)
                    {

                        Debug.LogWarning("Use of unimplemented function.");
                        */
                    /*
                    using (PooledBitReader reader = PooledBitReader.Get(stream))
                    {
                        ulong networkId = reader.ReadUInt64Packed();
                        ushort behaviourId = reader.ReadUInt16Packed();
                        ulong hash = reader.ReadUInt64Packed();

                        if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                        {
                            NetworkBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                            if (behaviour != null)
                            {
                                behaviour.OnRemoteClientRPC(hash, clientId, stream);
                            }
                        }
                    }
                    */
                    /*
                }

                private void HandleClientRPCRequest(ulong clientID, Stream stream, string channelName, SecuritySendFlags security)
                {

                    Debug.LogWarning("Use of unimplemented function.");*/
                    /*
                    using (PooledBitReader reader = PooledBitReader.Get(stream))
                    {
                        ulong networkId = reader.ReadUInt64Packed();
                        ushort behaviourId = reader.ReadUInt16Packed();
                        ulong hash = reader.ReadUInt64Packed();
                        ulong responseId = reader.ReadUInt64Packed();

                        if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                        {
                            NetworkBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                            if (behaviour != null)
                            {
                                object result = behaviour.OnRemoteClientRPC(hash, clientId, stream);

                                using (PooledBitStream responseStream = PooledBitStream.Get())
                                {
                                    using (PooledBitWriter responseWriter = PooledBitWriter.Get(responseStream))
                                    {
                                        responseWriter.WriteUInt64Packed(responseId);
                                        responseWriter.WriteObjectPacked(result);
                                    }

                                    InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_CLIENT_RPC_RESPONSE, channelName, responseStream, security, null);
                                }
                            }
                        }
                    }*/ /*
        }
       
        private void HandleClientRPCResponse(ulong clientID, Stream stream)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong responseId = reader.ReadUInt64Packed();

                if(ResponseMessageManager.ContainsKey(responseId))
                {
                    RpcResponseBase responseBase = ResponseMessageManager.GetByKey(responseId);

                    if(responseBase.ClientId != clientID) return;

                    ResponseMessageManager.Remove(responseId);

                    responseBase.IsDone = true;
                    responseBase.Result = reader.ReadObjectPacked(responseBase.Type);
                    responseBase.IsSuccessful = true;
                }
            }
        }*/

        private void HandleUnnamedMessage(ulong clientID, Stream stream)
        {
            CustomMessagingManager.InvokeUnnamedMessage(clientID, stream);
        }

        private void HandleNamedMessage(ulong clientID, Stream stream)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong hash = reader.ReadUInt64Packed();

                CustomMessagingManager.InvokeNamedMessage(hash, clientID, stream);
            }
        }

    } //Class
} //Namespace
