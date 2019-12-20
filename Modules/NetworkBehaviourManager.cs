using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using MLAPI.Serialization.Pooled;
using MLAPI.Hashing;
using MLAPI.Messaging;

using Isaac.Network.Messaging;
using Isaac.Network.Exceptions;
using System;

namespace Isaac.Network.Spawning
{
    public class NetworkBehaviourManager : NetworkModule
    {
        //Message types
        public byte addObjectMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte objectSuccessMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte destroyObjectMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte clientRPCMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte serverRPCMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte objectUnspawnMessageType { get; private set; } = (byte)MessageType.INVALID;

        public readonly List<NetworkBehaviourReference> networkBehaviours = new List<NetworkBehaviourReference>();
        //ulong = networkID
        public readonly Dictionary<ulong, NetworkBehaviourReference> networkBehaviourDictionary = new Dictionary<ulong, NetworkBehaviourReference>();

        public override Type[] dependencies => new Type[] { typeof(NetworkMessageHandler) };

        public delegate void NetworkBehaviourConnectedDelegate(ulong networkID, bool ownerCanUnspawn, bool destroyOnUnspawn);
        public delegate void NetworkBehaviourDisconnectedDelegate(bool ownerCanUnspawn, bool destroyOnUnspawn);
        public delegate void NetworkBehaviourRPCDelegate(ulong hash, ulong senderClientID, Stream stream);

        //private readonly Dictionary<NetworkBehaviour, NetworkBehaviourConnectedDelegate> m_ConnectedDelegates = new Dictionary<NetworkBehaviour, NetworkBehaviourConnectedDelegate>();
        //private readonly Dictionary<NetworkBehaviour, NetworkBehaviourDisconnectedDelegate> m_DisconnectedDelegates = new Dictionary<NetworkBehaviour, NetworkBehaviourDisconnectedDelegate>();

        //Network ID Management
        private ulong m_NetworkIDCounter = 0;
        private readonly Queue<ReleasedNetworkID> m_ReleasedNetworkIDs = new Queue<ReleasedNetworkID>();

        //The pending behaviours that are expecting connect to other behaviours across the network
        //Server ulong = Network ID | Client ulong = Unique Hash
        private readonly Dictionary<ulong, PendingNetworkBehaviour> m_LocalPendingBehaviours = new Dictionary<ulong, PendingNetworkBehaviour>();
        private readonly Dictionary<ulong, PendingNetworkBehaviour> m_RemotePendingBehaviours = new Dictionary<ulong, PendingNetworkBehaviour>();
        private readonly List<NetworkBehaviour> m_LocalPendingBehavioursList = new List<NetworkBehaviour>();

        //Used to check for hash collisions
        private readonly Dictionary<ulong, string> m_HashedStrings = new Dictionary<ulong, string>();

        private NetworkMessageHandler m_NetworkMessageHandler;

        public NetworkBehaviourManager()
        {

        }

        public override void Init(NetworkModule[] loadedDependencies)
        {
            if(m_NetworkMessageHandler == null)
            {
                m_NetworkMessageHandler = loadedDependencies[0] as NetworkMessageHandler;
                addObjectMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_ADD_OBJECT", HandleAddObjectMessage, NetworkMessageHandler.NetworkMessageReceiver.Client);
                objectSuccessMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_OBJECT_SUCCESS", HandleObjectSuccessMessage, NetworkMessageHandler.NetworkMessageReceiver.Server);
                destroyObjectMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_DESTRYOY_OBJECT", HandleDestroyObjectMessage, NetworkMessageHandler.NetworkMessageReceiver.Client);
                clientRPCMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_CLIENT_RPC", HandleClientRPCMessage, NetworkMessageHandler.NetworkMessageReceiver.Client);
                serverRPCMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_SERVER_RPC", HandleServerRPCMessage, NetworkMessageHandler.NetworkMessageReceiver.Server);
            }
            m_NetworkIDCounter = 0;
            networkBehaviours.Clear();
            networkBehaviourDictionary.Clear();
            m_ReleasedNetworkIDs.Clear();
            m_LocalPendingBehaviours.Clear();
            m_RemotePendingBehaviours.Clear();
            m_LocalPendingBehavioursList.Clear();
            //m_ConnectedDelegates.Clear();
            //m_DisconnectedDelegates.Clear();
            m_HashedStrings.Clear();
            SceneManager.sceneLoaded += OnSceneLoad;
        }

        public override void OnNetworkReady()
        {
            //Spawn all Network Behaviours that have NetworkBehaviour.spawnOnNetworkInit enabled.
            NetworkBehaviour[] allNetworkBehaviours = UnityEngine.Object.FindObjectsOfType<NetworkBehaviour>();
            for(int i = 0; i < allNetworkBehaviours.Length; i++)
            {
                if(allNetworkBehaviours[i].spawnOnNetworkInit)
                {
                    if(!networkManager.isServer && string.IsNullOrWhiteSpace(allNetworkBehaviours[i].uniqueID))
                    {
                        Debug.LogWarning("Skipping spawning target network behaviour on network init. It's unique ID is null or empty. Only the server can spawn Network Behaviours with a blank unique ID.", allNetworkBehaviours[i]);
                        continue;
                    }

                    allNetworkBehaviours[i].SpawnOnNetwork();
                }
            }
        }

        public override void Shutdown()
        {
            Debug.Log("Shutdown on Manager called");

            //Shutdown all Network Behaviours
            //KeyValuePair<NetworkBehaviour, NetworkBehaviourDisconnectedDelegate>[] pairs = m_DisconnectedDelegates.ToArray();
            for(int i = 0; i < networkBehaviours.Count; i++)
            {
                if(networkBehaviours[i].networkBehaviour.isNetworkSpawned)
                    networkBehaviours[i].networkBehaviour.UnspawnOnNetwork();
            }
            for(int i = 0; i < m_LocalPendingBehavioursList.Count; i++)
            {
                if(m_LocalPendingBehavioursList[i].isNetworkSpawned)
                    m_LocalPendingBehavioursList[i].UnspawnOnNetwork();
            }
        }

        public void SpawnOnNetwork(NetworkBehaviour behaviour, NetworkBehaviourConnectedDelegate connectedDelegate, NetworkBehaviourDisconnectedDelegate disconnectedDelegate, NetworkBehaviourRPCDelegate localRPCDelegate)
        {
            if(behaviour == null)
            {
                //Debug.LogError("Network Behaviour parameter cannot be null.");
                //return;
                throw new ArgumentNullException(nameof(behaviour));
            }

            if(behaviour.isNetworkReady)
            {
                //Debug.LogError("Target Network Behaviour is already spawned and connected on the network.", behaviour);
                //return;
                throw new ArgumentException("Network Behaviour is already spawned and connected on the network.", nameof(behaviour));
            }

            if(behaviour.isNetworkSpawned)
            {
                throw new ArgumentException("Network Behaviour is already spawned.", nameof(behaviour));
            }

            if(m_LocalPendingBehavioursList.Contains(behaviour))
            {
                //Debug.LogError("Target Network Behaviour is already spawned on the network.", behaviour);
                //return;
                throw new ArgumentException("Network Behaviour is already spawned on the network.", nameof(behaviour));
            }

            if(networkManager.isServer) //Server
            {
                ulong newNetworkID = GetNewNetworkID();
                ulong? uniqueHash = string.IsNullOrWhiteSpace(behaviour.uniqueID) ? null : behaviour.uniqueID?.GetStableHash(networkManager.config.rpcHashSize);
                
                if(uniqueHash != null)
                {
                    //Check if the unique hash has already been added.
                    if(m_LocalPendingBehaviours.ContainsKey(uniqueHash.Value))
                    {
                        if(m_HashedStrings[uniqueHash.Value] == behaviour.uniqueID)
                        {
                            Debug.LogError("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                            behaviour.UnspawnOnNetwork();
                            return;
                        }
                        else
                        {
                            //This occurs when 2 different strings hash to the same value from MLAPI.Hashing.GetStableHash.
                            Debug.LogError("A hash collision occurred. Either change the unique ID or increase the hash size in the config. '" + behaviour.uniqueID + "' and '" + m_HashedStrings[uniqueHash.Value] + "' both hashed to '" + uniqueHash + "'.");
                            behaviour.UnspawnOnNetwork();
                            return;
                        }
                    }
                    m_HashedStrings.Add(uniqueHash.Value, behaviour.uniqueID);
                }

                //m_ConnectedDelegates.Add(behaviour, connectedDelegate);
                //m_DisconnectedDelegates.Add(behaviour, disconnectedDelegate);
                m_LocalPendingBehaviours.Add(newNetworkID, new PendingNetworkBehaviour { uniqueHash = uniqueHash, isRemoteBehaviour = false, networkID = newNetworkID, reference = new NetworkBehaviourReference { networkBehaviour = behaviour, connectedDelegate = connectedDelegate, disconnectedDelegate = disconnectedDelegate, localRPCDelegate =  localRPCDelegate} });
                m_LocalPendingBehavioursList.Add(behaviour);


                //Since were the server, send to all clients the spawning Network Behaviour
                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        //Write behaviour info and type
                        writer.WriteUInt64Packed(newNetworkID);
                        writer.WriteUInt64Packed(behaviour.ownerClientID);
                        writer.WriteUInt64Packed(RPCTypeDefinition.GetHashFromType(behaviour.GetType()));
                        writer.WriteBool(uniqueHash != null);
                        if(uniqueHash != null)
                            writer.WriteUInt64Packed(uniqueHash.Value);
                        Debug.Log("Sending to clients the new behaviour " + behaviour.GetType());
                    }

                    MessageSender.SendToAll(addObjectMessageType, "NETWORK_INTERNAL", stream);
                }
            }
            else //Client
            {
                //Check if this is the Unique Behaviour being spawned in HandleAddObjectMessage where the Unique ID is blank and SpawnOnNetwork is called in Awake.;'
                if(m_TrackAwakeSpawns)
                {
                    m_BehavioursAwaitingSpawn.Enqueue(new NetworkBehaviourReference() { networkBehaviour = behaviour, connectedDelegate = connectedDelegate, disconnectedDelegate = disconnectedDelegate, localRPCDelegate = localRPCDelegate });
                    return;
                }

                //Check for valid unique ID
                //Only the server can create Network Behaviours with an empty uniqueID
                //or else the game will no longer be server-authenticated and secure.
                if(string.IsNullOrWhiteSpace(behaviour.uniqueID))
                {
                    throw new NotServerException("Only the server can create Network Behaviours with an empty uniqueID or else clients can do willy-nilly(technical term) across the network.");
                }

                ulong uniqueHash = behaviour.uniqueID.GetStableHash(networkManager.config.rpcHashSize);
                Debug.Log("Created Hash: " + uniqueHash);
                PendingNetworkBehaviour pendingBehaviour;
                if(m_RemotePendingBehaviours.TryGetValue(uniqueHash, out pendingBehaviour))
                {
                    OnObjectConnectSuccess(pendingBehaviour, pendingBehaviour.networkID);
                }
                else if(m_LocalPendingBehaviours.TryGetValue(uniqueHash, out pendingBehaviour))
                {
                    //Check if the unique hash has already been added by this client
                    if(m_HashedStrings[uniqueHash] == behaviour.uniqueID)
                    {
                        Debug.LogError("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                        behaviour.UnspawnOnNetwork();
                        return;
                    }
                    else
                    {
                        //This occurs when 2 different strings hash to the same value from MLAPI.Hashing.GetStableHash.
                        Debug.LogError("A hash collision occurred. Either change the unique ID or increase the hash size in the config. '" + behaviour.uniqueID + "' and '" + m_HashedStrings[uniqueHash] + "' both hashed to '" + uniqueHash + "'.");
                        behaviour.UnspawnOnNetwork();
                        return;
                    }
                }
                else
                {
                    //Add to pending
                    pendingBehaviour = new PendingNetworkBehaviour() { isRemoteBehaviour = false, uniqueHash = uniqueHash, ownerID = networkManager.serverID, networkID = 0, reference = new NetworkBehaviourReference { networkBehaviour = behaviour, connectedDelegate = connectedDelegate, disconnectedDelegate = disconnectedDelegate, localRPCDelegate = localRPCDelegate }};
                    m_LocalPendingBehavioursList.Add(pendingBehaviour.reference.networkBehaviour);
                    m_LocalPendingBehaviours.Add(uniqueHash, pendingBehaviour);
                    m_HashedStrings.Add(uniqueHash, behaviour.uniqueID);
                }
            }
        }

        public void UnspawnOnNetwork(NetworkBehaviour behaviour)
        {
            Debug.Log("UnspawnOnNetwork on Manager called");
            if(behaviour.Equals(null))
            {
                throw new ArgumentNullException(nameof(behaviour));
                //Debug.LogError("Network Behaviour parameter cannot be null.");
                //return;
            }

            if(behaviour == null)
            {
                Debug.Log("Destroyed.");
                //return;
            }

            if(!behaviour.isNetworkSpawned)
            {
                throw new ArgumentException("Unable to unspawn. Target behaviour is not spawned.", nameof(behaviour));
                //Debug.LogError("Unable to unspawn. Target behaviour is not spawned.", behaviour);
                //return;
            }

            if(networkManager.isServer)
            {
                //Send to clients
                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        writer.WriteUInt64Packed(behaviour.networkID);
                        writer.WriteBool(behaviour.destroyOnUnspawn);
                        Debug.Log("Sending to clients the destroyed message for behaviour " + behaviour.GetType());
                    }

                    MessageSender.SendToAll(destroyObjectMessageType, "NETWORK_INTERNAL", stream);
                }

                //Do unspawn
                DoLocalUnspawn(networkBehaviourDictionary[behaviour.networkID], behaviour.destroyOnUnspawn);
            }
            else //Client
            {
                if(behaviour.isOwner)
                {
                    if(behaviour.ownerCanUnspawn)
                    {
                        if(behaviour.isNetworkReady)
                            DoLocalUnspawn(networkBehaviourDictionary[behaviour.networkID], behaviour.destroyOnUnspawn);
                        else
                            DoLocalUnspawn(m_LocalPendingBehaviours.FirstOrDefault(b=>b.Value.reference.networkBehaviour == behaviour).Value,behaviour.destroyOnUnspawn);
                    }
                    else
                    {
                        throw new NotServerException("Network Behaviour is not allowed to be unspawned by the owner with ownerCanUnspawn set to false as a client.");
                    }
                }
                else
                {
                    Debug.LogError("Target Network Behaviour is not allowed to be unspawned by the client.");
                    return;
                }
            }
        }

        //Called to handle any local managing of unspawning a behaviour
        private void DoLocalUnspawn(NetworkBehaviourReference behaviourReference, bool destroy)
        {
            Debug.Log("Do Local Unspawn called");
            networkBehaviours.Remove(behaviourReference);
            networkBehaviourDictionary.Remove(behaviourReference.networkBehaviour.networkID);
            behaviourReference.disconnectedDelegate.Invoke(behaviourReference.networkBehaviour.ownerCanUnspawn, destroy);
            //m_HashedStrings.
        }

        private void DoLocalUnspawn(PendingNetworkBehaviour pendingBehaviour, bool destroy)
        {
            Debug.Log("Do Local Unspawn Pending called");
            if(networkManager.isServer)
                m_LocalPendingBehaviours.Remove(pendingBehaviour.networkID);
            else
                m_LocalPendingBehaviours.Remove(pendingBehaviour.uniqueHash.Value);
            m_LocalPendingBehavioursList.Remove(pendingBehaviour.reference.networkBehaviour);
            pendingBehaviour.reference.disconnectedDelegate.Invoke(pendingBehaviour.reference.networkBehaviour.ownerCanUnspawn, destroy);
            //m_HashedStrings.Remove(hash);
        }

        //Only the server can create new network IDs
        private ulong GetNewNetworkID()
        {
            //Try to recycle a network ID
            if(m_ReleasedNetworkIDs.Count > 0 && NetworkManager.Get().config.recycleNetworkIDs && (Time.unscaledTime - m_ReleasedNetworkIDs.Peek().releaseTime) >= NetworkManager.Get().config.networkIDRecycleDelay)
            {
                return m_ReleasedNetworkIDs.Dequeue().networkID;
            }

            //Brand new network ID
            m_NetworkIDCounter++;
            return m_NetworkIDCounter;
        }

        public override void OnClientConnect(ulong clientID)
        {
            if(networkManager.isServer)
            {
                //Send the newly connected clients all the spawned Network Behaviours
                for(int i = 0; i < networkBehaviours.Count; i++)
                {
                    //Since were the server, send to all clients the spawning Network Behaviour
                    using(PooledBitStream stream = PooledBitStream.Get())
                    {
                        using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                        {
                            //Write behaviour info and type
                            writer.WriteUInt64Packed(networkBehaviours[i].networkBehaviour.networkID);
                            writer.WriteUInt64Packed(networkBehaviours[i].networkBehaviour.ownerClientID);
                            writer.WriteUInt64Packed(RPCTypeDefinition.GetHashFromType(networkBehaviours[i].GetType()));
                            writer.WriteBool(networkBehaviours[i].networkBehaviour.ownerCanUnspawn);
                            writer.WriteBool(networkBehaviours[i].networkBehaviour.destroyOnUnspawn);
                            if(string.IsNullOrWhiteSpace(networkBehaviours[i].networkBehaviour.uniqueID))
                            {
                                writer.WriteBool(false);
                            }
                            else
                            {
                                writer.WriteBool(true);
                                writer.WriteUInt64Packed(networkBehaviours[i].networkBehaviour.uniqueID.GetStableHash(networkManager.config.rpcHashSize));
                            }
                            Debug.Log("Sending to new client the existing behaviour '" + networkBehaviours[i].GetType() + "'.");
                        }

                        MessageSender.SendToAll(addObjectMessageType, "NETWORK_INTERNAL", stream);
                    }
                }

                List<PendingNetworkBehaviour> values = m_LocalPendingBehaviours.Values.ToList();

                //Send any pending
                for(int i = 0; i < values.Count; i++)
                {
                    //Since were the server, send to all clients the spawning Network Behaviour
                    using(PooledBitStream stream = PooledBitStream.Get())
                    {
                        using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                        {
                            //Write behaviour info and type
                            writer.WriteUInt64Packed(values[i].networkID);
                            writer.WriteUInt64Packed(networkManager.serverID);
                            writer.WriteUInt64Packed(RPCTypeDefinition.GetHashFromType(values[i].reference.networkBehaviour.GetType()));
                            writer.WriteBool(values[i].ownerCanUnspawn);
                            writer.WriteBool(values[i].destroyOnUnspawn);
                            if(string.IsNullOrWhiteSpace(values[i].reference.networkBehaviour.uniqueID))
                            {
                                writer.WriteBool(false);
                            }
                            else
                            {
                                writer.WriteBool(true);
                                writer.WriteUInt64Packed(values[i].reference.networkBehaviour.uniqueID.GetStableHash(networkManager.config.rpcHashSize));
                            }
                            Debug.Log("Sending to new client the existing behaviour '" + values[i].reference.networkBehaviour.GetType() + "'.");
                        }

                        MessageSender.SendToAll(addObjectMessageType, "NETWORK_INTERNAL", stream);
                    }
                }
            }
        }

        #region Handle Messages

        //Used for when the client receives a blank unique ID object and is in the process of called SpawnOnNetwork.
        private bool m_TrackAwakeSpawns = false;
        private Queue<NetworkBehaviourReference> m_BehavioursAwaitingSpawn = new Queue<NetworkBehaviourReference>();

        public void HandleAddObjectMessage(ulong clientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed(); //Network ID
                ulong ownerID = reader.ReadUInt64Packed(); //Owner
                Type behaviourType = RPCTypeDefinition.GetTypeFromHash(reader.ReadUInt64Packed());
                bool ownerCanUnspawn = reader.ReadBool();
                bool destroyOnUnspawn = reader.ReadBool();
                bool hasUniqueHash = reader.ReadBool();
                //if(networkManager.enableLogging)
                    Debug.Log("Received add object event from server. Object: " + behaviourType.ToString() + " | Network ID: " + networkID + " | Owner: " + ownerID + " | Has Hash: " + hasUniqueHash + " | Owner Can Unspawn: " + ownerCanUnspawn + " | Destroy On Unspawn: " + destroyOnUnspawn);
                if(hasUniqueHash)
                {
                    Debug.Log("Received add object with Network ID: " + networkID);
                    ulong uniqueHash = reader.ReadUInt64Packed(); //Unique hash ID
                    Debug.Log("Received Hash: " + uniqueHash);
                    PendingNetworkBehaviour pendingBehaviour;
                    if(m_LocalPendingBehaviours.TryGetValue(uniqueHash, out pendingBehaviour))
                    {
                        if(pendingBehaviour.reference.networkBehaviour.GetType() != behaviourType)
                        {
                            Debug.LogError("Received add object message where the remote network behaviour type(" + behaviourType.ToString() + ") does not match up with local network behaviour type (" + pendingBehaviour.reference.networkBehaviour.GetType() + ") with same unique ID(" + pendingBehaviour.reference.networkBehaviour.uniqueID + ").", pendingBehaviour.reference.networkBehaviour);
                            return;
                        }
                        OnObjectConnectSuccess(pendingBehaviour, networkID);
                    }
                    else if(m_RemotePendingBehaviours.ContainsKey(uniqueHash))
                    {
                        Debug.LogError("Received duplicate 'add object' message for hash '" + uniqueHash + "'.");
                        return;
                    }
                    else
                    {
                        m_RemotePendingBehaviours.Add(uniqueHash, new PendingNetworkBehaviour() { isRemoteBehaviour = true, uniqueHash = uniqueHash, ownerID = networkManager.serverID, networkID = networkID, ownerCanUnspawn = ownerCanUnspawn, destroyOnUnspawn = destroyOnUnspawn });
                    }
                }
                else //No unique hash
                {
                    Debug.Log("Received blank add object with Network ID: " + networkID);

                    //Build network behaviour
                    GameObject behaviourObject = new GameObject("Server Network Object");
                    
                    //All this stuff just in case the instantiate behaviour also instantiates other network behaviours in its awake function
                    m_TrackAwakeSpawns = true; 

                    NetworkBehaviour serverBehaviour = (NetworkBehaviour)behaviourObject.AddComponent(behaviourType);
                    
                    m_TrackAwakeSpawns = false;

                    while(m_BehavioursAwaitingSpawn.Count > 0)
                    {
                        NetworkBehaviourReference reference = m_BehavioursAwaitingSpawn.Dequeue();
                        if(reference.networkBehaviour == serverBehaviour)
                        {
                            OnObjectConnectSuccess(new PendingNetworkBehaviour() { reference = reference, networkID = networkID, ownerID = ownerID, destroyOnUnspawn = destroyOnUnspawn, isRemoteBehaviour = false, uniqueHash = null, ownerCanUnspawn = ownerCanUnspawn }, networkID);
                        }
                        SpawnOnNetwork(reference.networkBehaviour, reference.connectedDelegate, reference.disconnectedDelegate, reference.localRPCDelegate);
                    }
                }
            }
        }

        private void HandleObjectSuccessMessage(ulong clientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                PendingNetworkBehaviour pendingBehaviour;
                ulong networkID = reader.ReadUInt64Packed();
                Debug.Log("Received object success with ID: " + networkID);
                if(m_LocalPendingBehaviours.TryGetValue(networkID, out pendingBehaviour))
                {
                    m_LocalPendingBehaviours.Remove(networkID);
                    m_LocalPendingBehavioursList.Remove(pendingBehaviour.reference.networkBehaviour);
                    networkBehaviours.Add(pendingBehaviour.reference);
                    networkBehaviourDictionary.Add(networkID, pendingBehaviour.reference);
                    pendingBehaviour.reference.connectedDelegate.Invoke(networkID, pendingBehaviour.reference.networkBehaviour.ownerCanUnspawn, pendingBehaviour.reference.networkBehaviour.destroyOnUnspawn);
                    //m_ConnectedDelegates[pendingBehaviour.networkBehaviour].Invoke(networkID, pendingBehaviour.networkBehaviour.ownerCanUnspawn, pendingBehaviour.networkBehaviour.destroyOnUnspawn);
                    //m_OnNetworkBehaviourConnected?.Invoke(pendingBehaviour.networkBehaviour, networkID);
                }
                else
                {
                    //Check if network ID is being used
                    if(!networkBehaviourDictionary.ContainsKey(networkID))
                        Debug.LogError("Received object success message with unknown network ID '" + networkID + "'.");

                    //Do nothing were already spawned and connected on the network.
                }
            }
        }

        public void HandleDestroyObjectMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received unspawn message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                NetworkBehaviourReference targetBehaviour;
                ulong networkID = reader.ReadUInt64Packed();
                bool destroy = reader.ReadBool();
                if(!networkBehaviourDictionary.TryGetValue(networkID, out targetBehaviour))
                {
                    Debug.LogError("Target Network Behaviour with Network ID '" + networkID.ToString() + "' was not found. Nothing was unspawned.");
                    return;
                }

                if(networkManager.isServer && networkManager.clientID != clientID)
                {
                    //Check if the client sending this destroy message is the owner of the object.
                    if(targetBehaviour.networkBehaviour.ownerClientID != clientID)
                    {
                        Debug.LogError("Received message from client " + clientID + " trying to unspawn a Network Behaviour when they are not the owner of the Network Behaviour.");
                        return;
                    }
                    else if(!targetBehaviour.networkBehaviour.ownerCanUnspawn)
                    {
                        Debug.LogError("Received message from client " + clientID + " trying to unspawn a Network Behaviour when they are the owner and not allowed to destroy the Network Behaviour.");
                        return;
                    }
                }

                DoLocalUnspawn(targetBehaviour, destroy || targetBehaviour.networkBehaviour.destroyOnUnspawn);
            }
        }

        public void HandleClientRPCMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received Client RPC Message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                
            }
        }

        public void HandleServerRPCMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received Server RPC Message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                ulong hash = reader.ReadUInt64Packed();

                NetworkBehaviourReference targetBehaviour;
                if(!networkBehaviourDictionary.TryGetValue(networkID, out targetBehaviour))
                {
                    Debug.LogError("Received Server RPC message but the specified Network ID '" + networkID + "' is not associated with a spawned and ready Network Behaviour.");
                    return;
                }

                //TODO Check if the client is even allowed to send this message. Such as non-owner can invoke or is even visible to the specific client.
                
            }
        }

        #endregion Handle Messages

        /// <summary>
        ///Called when the client successfully connects Network Behaviours. Involves switching pending Network Behaviours to active and sending the server a success message.
        ///(Client only function)
        ///</summary>
        private void OnObjectConnectSuccess(PendingNetworkBehaviour pendingBehaviour, ulong networkID)
        {
            if(pendingBehaviour.uniqueHash != null)
            {
                //Clean up pending
                m_LocalPendingBehaviours.Remove(pendingBehaviour.uniqueHash.Value);
                m_LocalPendingBehavioursList.Remove(pendingBehaviour.reference.networkBehaviour);
                m_HashedStrings.Remove(pendingBehaviour.uniqueHash.Value);
            }
            networkBehaviours.Add(pendingBehaviour.reference);
            networkBehaviourDictionary.Add(networkID, pendingBehaviour.reference);

            //Send confirm connection
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    //if(networkManager.enableLogging)
                        Debug.Log("Sending success of add object with Network ID: " + networkID);
                }
            
                MessageSender.Send(networkManager.serverID, objectSuccessMessageType, "NETWORK_INTERNAL", stream);
            }

            //Let our local behaviour know
            pendingBehaviour.reference.connectedDelegate.Invoke(networkID, pendingBehaviour.ownerCanUnspawn, pendingBehaviour.destroyOnUnspawn);
            //m_ConnectedDelegates[pendingBehaviour.networkBehaviour].Invoke(networkID, pendingBehaviour.ownerCanUnspawn, pendingBehaviour.destroyOnUnspawn);
            //m_OnNetworkBehaviourConnected?.Invoke(pendingBehaviour.networkBehaviour, networkID);
        }


        private readonly List<GameObject> m_SceneGameObjects = new List<GameObject>();
        private void OnSceneLoad(Scene scene, LoadSceneMode loadSceneMode)
        {
            while(m_SceneGameObjects.Count < scene.rootCount)
            {
                m_SceneGameObjects.Add(null);
            }
            scene.GetRootGameObjects(m_SceneGameObjects);
            for(int i = 0; i < scene.rootCount; i++)
            {
                //TODO This needs to be optimized to not allocate garbage.
                NetworkBehaviour[] foundBehaviours = m_SceneGameObjects[i].GetComponentsInChildren<NetworkBehaviour>(false);
                for(int h = 0; h < foundBehaviours.Length; h++)
                {
                    if(foundBehaviours[h].spawnOnSceneLoad && !foundBehaviours[h].isNetworkSpawned)
                    {
                        if(!networkManager.isServer && string.IsNullOrWhiteSpace(foundBehaviours[h].uniqueID))
                        {

                            Debug.LogWarning("Skipping spawning target Network Behaviour on scene load. It's unique ID is null or empty. Only the server can spawn Network Behaviours with a blank unique ID.", foundBehaviours[h]);
                            continue;
                        }
                        foundBehaviours[h].SpawnOnNetwork();
                    }
                }
            }
            new ReleasedNetworkID();
        }

        private struct ReleasedNetworkID
        {
            public ulong networkID;
            public float releaseTime;


        } //Struct

        public struct NetworkBehaviourReference
        {
            public NetworkBehaviour networkBehaviour;
            public NetworkBehaviourConnectedDelegate connectedDelegate;
            public NetworkBehaviourDisconnectedDelegate disconnectedDelegate;
            public NetworkBehaviourRPCDelegate localRPCDelegate;
            public int listIndex;
        }

        private struct PendingNetworkBehaviour
        {
            public bool isRemoteBehaviour; //True if the reference variable's networkBehaviour is null
            public NetworkBehaviourReference reference; //Local only
            public ulong networkID; //Server local, Client remote
            public ulong? uniqueHash; //If this is null it means it is a server created behaviour
            public ulong ownerID;
            public bool ownerCanUnspawn;
            public bool destroyOnUnspawn;
        } //Struct
    } //Class

} //Namespace
