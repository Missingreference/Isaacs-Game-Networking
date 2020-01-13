using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

using UnityEngine;
using UnityEngine.SceneManagement;

using MLAPI.Serialization.Pooled;
using MLAPI.Hashing;
using MLAPI.Messaging;

using Isaac.Network.Messaging;
using Isaac.Network.Exceptions;

namespace Isaac.Network.Spawning
{
    public class NetworkBehaviourManager : NetworkModule
    {
        //Message types
        public byte spawnMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte unspawnMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte objectSuccessMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte clientRPCMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte serverRPCMessageType { get; private set; } = (byte)MessageType.INVALID;
        public byte ownerChangeMessageType { get; private set; } = (byte)MessageType.INVALID;


        public override Type[] dependencies => new Type[] { typeof(NetworkMessageHandler) };

        public delegate void ServerBehaviourConnectedDelegate(ulong networkID, ulong clientID, List<ulong> observers);
        public delegate void ClientBehaviourConnectedDelegate(ulong networkID, ulong ownerID, bool ownerCanUnspawn, bool destroyOnUnspawn);
        public delegate void BehaviourDisconnectedDelegate(bool ownerCanUnspawn, bool destroyOnUnspawn);
        public delegate void NetworkBehaviourRPCDelegate(ulong hash, ulong senderClientID, Stream stream);
        public delegate void OwnerChangeDelegate(ulong newOwner, bool ownerCanUnspawn);

        //Network Behaviours
        public readonly List<NetworkBehaviourReference> m_NetworkBehaviours = new List<NetworkBehaviourReference>();
        //ulong key = networkID
        public readonly Dictionary<ulong, NetworkBehaviourReference> m_NetworkBehaviourDictionary = new Dictionary<ulong, NetworkBehaviourReference>();

        //Network ID Management
        private ulong m_NetworkIDCounter = 0;
        private readonly Queue<ReleasedNetworkID> m_ReleasedNetworkIDs = new Queue<ReleasedNetworkID>();

        //The pending behaviours that are expecting connect to other behaviours across the network. Client only have pending.
        //Client ulong = Unique Hash
        private readonly Dictionary<ulong, PendingNetworkBehaviour> m_LocalPendingBehaviours = new Dictionary<ulong, PendingNetworkBehaviour>();
        private readonly Dictionary<ulong, PendingNetworkBehaviour> m_RemotePendingBehavioursHashes = new Dictionary<ulong, PendingNetworkBehaviour>();
        private readonly Dictionary<ulong, PendingNetworkBehaviour> m_RemotePendingBehaviours = new Dictionary<ulong, PendingNetworkBehaviour>();
        private readonly List<NetworkBehaviour> m_LocalPendingBehavioursList = new List<NetworkBehaviour>();

        //Used to check for hash collisions
        private readonly Dictionary<ulong, string> m_HashedStrings = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, NetworkBehaviour> m_BehaviourByHashedID = new Dictionary<ulong, NetworkBehaviour>();

        private NetworkMessageHandler m_NetworkMessageHandler;

        public NetworkBehaviourManager()
        {

        }

        public override void Init(NetworkModule[] loadedDependencies)
        {
            if(m_NetworkMessageHandler == null)
            {
                m_NetworkMessageHandler = loadedDependencies[0] as NetworkMessageHandler;
                spawnMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_SPAWN_OBJECT", HandleSpawnMessage, NetworkMessageHandler.NetworkMessageReceiver.Client);
                unspawnMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_UNSPAWN_OBJECT", HandleUnspawnMessage, NetworkMessageHandler.NetworkMessageReceiver.Client);
                objectSuccessMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_OBJECT_SUCCESS", HandleObjectSuccessMessage, NetworkMessageHandler.NetworkMessageReceiver.Server);
                clientRPCMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_CLIENT_RPC", HandleClientRPCMessage, NetworkMessageHandler.NetworkMessageReceiver.Client);
                serverRPCMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_SERVER_RPC", HandleServerRPCMessage, NetworkMessageHandler.NetworkMessageReceiver.Server);
                ownerChangeMessageType = m_NetworkMessageHandler.RegisterMessageType("NETWORK_OWNER_CHANGE", HandleOwnerChangeMessage, NetworkMessageHandler.NetworkMessageReceiver.Both);
            }
            m_NetworkIDCounter = 0;
            m_NetworkBehaviours.Clear();
            m_NetworkBehaviourDictionary.Clear();
            m_ReleasedNetworkIDs.Clear();
            m_LocalPendingBehaviours.Clear();
            m_RemotePendingBehaviours.Clear();
            m_RemotePendingBehavioursHashes.Clear();
            m_LocalPendingBehavioursList.Clear();
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
            //Shutdown all Network Behaviours
            for(int i = 0; i < m_NetworkBehaviours.Count; i++)
            {
                if(m_NetworkBehaviours[i].networkBehaviour.isNetworkSpawned)
                    DoUnspawnOnNetwork(m_NetworkBehaviours[i].networkBehaviour);
            }
            for(int i = 0; i < m_LocalPendingBehavioursList.Count; i++)
            {
                if(m_LocalPendingBehavioursList[i].isNetworkSpawned)
                    DoUnspawnOnNetwork(m_LocalPendingBehavioursList[i]);
            }
            SceneManager.sceneLoaded -= OnSceneLoad;
        }

        //Server only
        public void SpawnOnNetworkServer(NetworkBehaviour behaviour, ServerBehaviourConnectedDelegate connectedCallback, BehaviourDisconnectedDelegate disconnectCallback, NetworkBehaviourRPCDelegate localRPCCallback, OwnerChangeDelegate ownerChangeCallback, ulong owner, List<ulong> observers)
        {
            if(behaviour == null || !behaviour.isNetworkSpawned || behaviour.isNetworkReady)
            {
                throw new InvalidOperationException("NetworkBehaviourManager.SpawnOnNetworkServer is only allowed to be called internally by a Network Behaviour.");
            }

            ulong newNetworkID = GetNewNetworkID();

            if(!string.IsNullOrWhiteSpace(behaviour.uniqueID))
            {
                ulong uniqueHash = behaviour.uniqueID.GetStableHash(networkManager.config.rpcHashSize);
                if(m_BehaviourByHashedID.TryGetValue(uniqueHash, out NetworkBehaviour otherBehaviour))
                {
                    if(!BehaviourWasDestroyed(otherBehaviour))
                    {
                        throw new NetworkException("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                    }
                }

                if(m_HashedStrings.TryGetValue(uniqueHash, out string otherUniqueID))
                {
                    if(otherUniqueID == behaviour.uniqueID)
                    {
                        throw new NetworkException("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                    }
                    else
                    {
                        //This occurs when 2 different strings hash to the same value from MLAPI.Hashing.GetStableHash.
                        throw new NetworkException("A hash collision occurred. Either change the unique ID or increase the hash size in the config. '" + behaviour.uniqueID + "' and '" + otherUniqueID + "' both hashed to '" + uniqueHash + "'.");
                    }
                }
                m_HashedStrings.Add(uniqueHash, behaviour.uniqueID);
                m_BehaviourByHashedID.Add(uniqueHash, behaviour);
            }

            m_NetworkBehaviours.Add(new NetworkBehaviourReference() { networkBehaviour = behaviour, connectedServerCallback = connectedCallback, disconnectedDelegate = disconnectCallback, localRPCDelegate = localRPCCallback, ownerChangeDelegate = ownerChangeCallback });
            m_NetworkBehaviourDictionary.Add(newNetworkID, m_NetworkBehaviours[m_NetworkBehaviours.Count - 1]);
            
            connectedCallback.Invoke(newNetworkID, networkManager.serverID, observers);
        }

        //Client only
        public void SpawnOnNetworkClient(NetworkBehaviour behaviour, ClientBehaviourConnectedDelegate connectedCallback, BehaviourDisconnectedDelegate disconnectedCallback, NetworkBehaviourRPCDelegate localRPCCallback, OwnerChangeDelegate ownerChangeCallback)
        {
            if(behaviour == null || !behaviour.isNetworkSpawned || behaviour.isNetworkReady || m_LocalPendingBehavioursList.Contains(behaviour) || string.IsNullOrWhiteSpace(behaviour.uniqueID))
            {
                throw new InvalidOperationException("NetworkBehaviourManager.SpawnNetworkClient is only allowed to be called internally by a Network Behaviour.");
            }

            //Check if this is the Unique Behaviour being spawned in HandleAddObjectMessage where the Unique ID is blank and SpawnOnNetwork is called in Awake.
            if(m_TrackAwakeSpawns)
            {
                m_BehavioursAwaitingSpawn.Enqueue(new NetworkBehaviourReference() { networkBehaviour = behaviour, connectedClientCallback = connectedCallback, disconnectedDelegate = disconnectedCallback, localRPCDelegate = localRPCCallback, ownerChangeDelegate = ownerChangeCallback });
                return;
            }

            ulong uniqueHash = behaviour.uniqueID.GetStableHash(networkManager.config.rpcHashSize);

            if(m_RemotePendingBehavioursHashes.TryGetValue(uniqueHash, out PendingNetworkBehaviour pendingBehaviour))
            {
                m_RemotePendingBehavioursHashes.Remove(uniqueHash);
                m_RemotePendingBehaviours.Remove(pendingBehaviour.networkID);
                OnObjectConnectSuccess(new NetworkBehaviourReference() { networkBehaviour = behaviour, connectedClientCallback = connectedCallback, disconnectedDelegate = disconnectedCallback, localRPCDelegate = localRPCCallback, ownerChangeDelegate = ownerChangeCallback }, pendingBehaviour.networkID, pendingBehaviour.ownerID, pendingBehaviour.ownerCanUnspawn, pendingBehaviour.destroyOnUnspawn);
            }
            else if(m_LocalPendingBehaviours.TryGetValue(uniqueHash, out pendingBehaviour))
            {
                Debug.Log("Called1");
                //Check if the unique hash has already been added by this client
                if(m_BehaviourByHashedID.TryGetValue(uniqueHash, out NetworkBehaviour otherBehaviour))
                {
                    Debug.Log("Called2");
                    if(!BehaviourWasDestroyed(otherBehaviour))
                    {
                        throw new NetworkException("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                    }
                }

                if(m_HashedStrings.TryGetValue(uniqueHash, out string otherUniqueID))
                {
                    Debug.Log("Called3");
                    if(otherUniqueID == behaviour.uniqueID)
                    {
                        throw new NetworkException("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                    }
                    else
                    {
                        //This occurs when 2 different strings hash to the same value from MLAPI.Hashing.GetStableHash.
                        throw new NetworkException("A hash collision occurred. Either change the unique ID or increase the hash size in the config. '" + behaviour.uniqueID + "' and '" + otherUniqueID + "' both hashed to '" + uniqueHash + "'.");
                    }
                }
            }
            else
            {
                Debug.Log("Called4");
                if(m_BehaviourByHashedID.TryGetValue(uniqueHash, out NetworkBehaviour otherBehaviour))
                {
                    Debug.Log("Called5");
                    if(!BehaviourWasDestroyed(otherBehaviour))
                    {
                        throw new NetworkException("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                    }
                }

                if(m_HashedStrings.TryGetValue(uniqueHash, out string otherUniqueID))
                {
                    Debug.Log("Called6");
                    if(otherUniqueID == behaviour.uniqueID)
                    {
                        throw new NetworkException("A Network Behaviour already has the unique ID '" + behaviour.uniqueID + "'.");
                    }
                    else
                    {
                        //This occurs when 2 different strings hash to the same value from MLAPI.Hashing.GetStableHash.
                        throw new NetworkException("A hash collision occurred. Either change the unique ID or increase the hash size in the config. '" + behaviour.uniqueID + "' and '" + otherUniqueID + "' both hashed to '" + uniqueHash + "'.");
                    }
                }
                Debug.Log("Called7");

                //Add to pending
                pendingBehaviour = new PendingNetworkBehaviour() { isRemoteBehaviour = false, uniqueHash = uniqueHash, ownerID = networkManager.serverID, networkID = 0, reference = new NetworkBehaviourReference { networkBehaviour = behaviour, connectedClientCallback = connectedCallback, disconnectedDelegate = disconnectedCallback, localRPCDelegate = localRPCCallback, ownerChangeDelegate = ownerChangeCallback } };
                m_LocalPendingBehavioursList.Add(pendingBehaviour.reference.networkBehaviour);
                m_LocalPendingBehaviours.Add(uniqueHash, pendingBehaviour);
                m_HashedStrings.Add(uniqueHash, behaviour.uniqueID);
                m_BehaviourByHashedID.Add(uniqueHash, behaviour);
            }
        }

        public void UnspawnOnNetwork(NetworkBehaviour behaviour)
        {
            if(behaviour == null || !behaviour.isNetworkSpawned || (!networkManager.isServer && !(behaviour.isOwner && behaviour.ownerCanUnspawn)))
            {
                throw new InvalidOperationException("NetworkBehaviourManager.UnspawnOnNetwork is only allowed to be called internally by a Network Behaviour.");
            }

            DoUnspawnOnNetwork(behaviour);
        }

        private void DoUnspawnOnNetwork(NetworkBehaviour behaviour)
        {
            if(networkManager.isServer)
            {
                //Send to clients
                behaviour.NetworkHideAll();
                /*
                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        writer.WriteUInt64Packed(behaviour.networkID);
                        writer.WriteBool(behaviour.destroyOnUnspawn);
                        Debug.Log("Sending to clients the destroy message for behaviour " + behaviour.GetType());
                    }

                    MessageSender.SendToAll(unspawnMessageType, networkManager.networkInternalChannel, stream);
                }*/

                //Do unspawn
                if(behaviour.isNetworkReady)
                {
                    DoLocalUnspawn(m_NetworkBehaviourDictionary[behaviour.networkID], behaviour.destroyOnUnspawn);
                }
                else
                {

                }
            }
            else //Client
            {
                if(behaviour.isNetworkReady)
                    DoLocalUnspawn(m_NetworkBehaviourDictionary[behaviour.networkID], behaviour.destroyOnUnspawn);
                else
                    DoLocalUnspawn(m_LocalPendingBehaviours.FirstOrDefault(b => b.Value.reference.networkBehaviour == behaviour).Value, behaviour.destroyOnUnspawn);
            }
        }

        //Called to handle any local managing of unspawning a behaviour
        private void DoLocalUnspawn(NetworkBehaviourReference behaviourReference, bool destroy)
        {
            Debug.Log("Do Local Unspawn called");
            m_NetworkBehaviours.Remove(behaviourReference);
            m_NetworkBehaviourDictionary.Remove(behaviourReference.networkBehaviour.networkID);
            if(!string.IsNullOrWhiteSpace(behaviourReference.networkBehaviour.uniqueID))
            {
                ulong hash = behaviourReference.networkBehaviour.uniqueID.GetStableHash(networkManager.config.rpcHashSize);
                m_HashedStrings.Remove(hash);
                m_BehaviourByHashedID.Remove(hash);
            }
            behaviourReference.disconnectedDelegate.Invoke(behaviourReference.networkBehaviour.ownerCanUnspawn, destroy);
        }

        //Do unspawn on pending behaviour
        //Client only
        private void DoLocalUnspawn(PendingNetworkBehaviour pendingBehaviour, bool destroy)
        {
            Debug.Log("Do Local Unspawn Pending called");
            m_LocalPendingBehaviours.Remove(pendingBehaviour.uniqueHash.Value);

            if(pendingBehaviour.uniqueHash != null)
            {
                m_HashedStrings.Remove(pendingBehaviour.uniqueHash.Value);
                m_BehaviourByHashedID.Remove(pendingBehaviour.uniqueHash.Value);
            }
            m_LocalPendingBehavioursList.Remove(pendingBehaviour.reference.networkBehaviour);

            pendingBehaviour.reference.disconnectedDelegate.Invoke(pendingBehaviour.reference.networkBehaviour.ownerCanUnspawn, destroy);
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
                for(int i = 0; i < m_NetworkBehaviours.Count; i++)
                {
                    if(!m_NetworkBehaviours[i].networkBehaviour.networkShowOnNewClients) continue;
                    Debug.Log("Sending to new client the existing behaviour '" + m_NetworkBehaviours[i].networkBehaviour.GetType() + "'.");
                    m_NetworkBehaviours[i].networkBehaviour.NetworkShow(clientID);
                }
            }
        }

        public override void OnClientDisconnect(ulong clientID)
        {
            if(networkManager.isServer)
            {
                for(int i = 0; i < m_NetworkBehaviours.Count; i++)
                {
                    m_NetworkBehaviours[i].networkBehaviour.NetworkHide(clientID);
                }
            }
        }

        #region Handle Messages

        //Used for when the client receives a blank unique ID object and is in the process of called SpawnOnNetwork.
        private bool m_TrackAwakeSpawns = false;
        private Queue<NetworkBehaviourReference> m_BehavioursAwaitingSpawn = new Queue<NetworkBehaviourReference>();

        public void HandleSpawnMessage(ulong clientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed(); //Network ID
                ulong ownerID = reader.ReadUInt64Packed(); //Owner
                Type behaviourType = RPCTypeDefinition.GetTypeFromHash(reader.ReadUInt64Packed());
                bool hasUniqueHash = reader.ReadBool();
                ulong uniqueHash = 0;
                if(hasUniqueHash)
                {
                    uniqueHash = reader.ReadUInt64Packed();
                }
                bool ownerCanUnspawn = reader.ReadBool();
                bool destroyOnUnspawn = reader.ReadBool();

                if(networkManager.enableLogging)
                {
                    string s = "Received add object event from server. Object: " + behaviourType.ToString() + " | Network ID: " + networkID + " | Owner: " + ownerID + " | Has Unique Hash: " + hasUniqueHash + " | ";
                    if(hasUniqueHash) s += uniqueHash + " | ";
                    s += "Owner Can Unspawn: " + ownerCanUnspawn + " | Destroy On Unspawn: " + destroyOnUnspawn;
                    Debug.Log(s);
                }
                
                if(hasUniqueHash)
                {
                    if(m_LocalPendingBehaviours.TryGetValue(uniqueHash, out PendingNetworkBehaviour pendingBehaviour))
                    {
                        if(pendingBehaviour.reference.networkBehaviour.GetType() != behaviourType)
                        {
                            Debug.LogError("Received add object message where the remote network behaviour type(" + behaviourType.ToString() + ") does not match up with local network behaviour type (" + pendingBehaviour.reference.networkBehaviour.GetType() + ") with same unique ID(" + pendingBehaviour.reference.networkBehaviour.uniqueID + ").", pendingBehaviour.reference.networkBehaviour);
                            return;
                        }

                        //Clean up pending
                        m_LocalPendingBehaviours.Remove(uniqueHash);
                        m_LocalPendingBehavioursList.Remove(pendingBehaviour.reference.networkBehaviour);

                        OnObjectConnectSuccess(pendingBehaviour.reference, networkID, ownerID, ownerCanUnspawn, destroyOnUnspawn);
                    }
                    else if(m_RemotePendingBehavioursHashes.ContainsKey(uniqueHash))
                    {
                        Debug.LogError("Received duplicate 'add object' message for hash '" + uniqueHash + "'.");
                        return;
                    }
                    else if(m_RemotePendingBehaviours.ContainsKey(networkID))
                    {
                        Debug.LogError("Recevied duplicate 'add object' message for network ID '" + networkID + "'.");
                        return;
                    }
                    else
                    {
                        PendingNetworkBehaviour pendingBehaviourReference = new PendingNetworkBehaviour() { isRemoteBehaviour = true, uniqueHash = uniqueHash, ownerID = ownerID, networkID = networkID, ownerCanUnspawn = ownerCanUnspawn, destroyOnUnspawn = destroyOnUnspawn };
                        m_RemotePendingBehavioursHashes.Add(uniqueHash, pendingBehaviourReference);
                        m_RemotePendingBehaviours.Add(networkID, pendingBehaviourReference);
                    }
                }
                else //No unique hash
                {
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
                            OnObjectConnectSuccess(reference, networkID, ownerID, ownerCanUnspawn, destroyOnUnspawn);
                        }
                        SpawnOnNetworkClient(reference.networkBehaviour, reference.connectedClientCallback, reference.disconnectedDelegate, reference.localRPCDelegate, reference.ownerChangeDelegate);
                    }
                }
            }
        }

        private void HandleObjectSuccessMessage(ulong clientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                Debug.Log("Received object success with ID: " + networkID);
                if(m_NetworkBehaviourDictionary.TryGetValue(networkID, out NetworkBehaviourReference behaviourReference))
                {
                    if(BehaviourWasDestroyed(behaviourReference.networkBehaviour)) return;

                    behaviourReference.connectedServerCallback.Invoke(networkID, clientID, null);
                }
                else
                {
                    Debug.LogError("Received object success message with unknown network ID '" + networkID + "'.");
                }
            }
        }

        public void HandleUnspawnMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received unspawn message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                bool destroy = reader.ReadBool();

                if(m_NetworkBehaviourDictionary.TryGetValue(networkID, out NetworkBehaviourReference targetBehaviour))
                {
                    //Unspawn a connected Network Behaviour
                    DoLocalUnspawn(targetBehaviour, destroy || targetBehaviour.networkBehaviour.destroyOnUnspawn);
                    return;
                }
                else if(m_RemotePendingBehaviours.TryGetValue(networkID, out PendingNetworkBehaviour pendingBehaviour))
                {
                    m_RemotePendingBehaviours.Remove(networkID);
                    m_RemotePendingBehavioursHashes.Remove(pendingBehaviour.uniqueHash.Value);
                    return;
                }

                //No behaviour found connected or pending.
                Debug.LogError("Target Network Behaviour with Network ID '" + networkID.ToString() + "' was not found. Nothing was unspawned.");
            }
        }

        public void HandleClientRPCMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received Client RPC Message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                ulong methodHash = reader.ReadUInt64Packed();

                if(!m_NetworkBehaviourDictionary.TryGetValue(networkID, out NetworkBehaviourReference behaviourReference))
                {
                    Debug.LogError("Received Client RPC message but the specified network ID '" + networkID + "' is not associated with a spawned and ready Network Behaviour.");
                    return;
                }

                if(BehaviourWasDestroyed(behaviourReference.networkBehaviour)) return;

                behaviourReference.localRPCDelegate.Invoke(methodHash, clientID, stream);
            }
        }

        public void HandleServerRPCMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received Server RPC Message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                ulong hash = reader.ReadUInt64Packed();

                if(!m_NetworkBehaviourDictionary.TryGetValue(networkID, out NetworkBehaviourReference behavoiurReference))
                {
                    Debug.LogError("Received Server RPC message but the specified network ID '" + networkID + "' is not associated with a spawned and ready Network Behaviour.");
                    return;
                }

                if(BehaviourWasDestroyed(behavoiurReference.networkBehaviour)) return;

                //TODO Check if the client is even allowed to send this message. Such as non-owner can invoke or is even visible to the specific client.
                behavoiurReference.localRPCDelegate.Invoke(hash, clientID, stream);
            }
        }

        public void HandleOwnerChangeMessage(ulong clientID, Stream stream, float receiveTime)
        {
            Debug.Log("Received Onwer Change Message");
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkID = reader.ReadUInt64Packed();
                ulong newOwner = reader.ReadUInt64Packed();
                bool ownerCanUnspawn = reader.ReadBool();

                if(networkManager.isServer)
                {
                    if(!m_NetworkBehaviourDictionary.TryGetValue(networkID, out NetworkBehaviourReference behaviourReference))
                    {
                        Debug.LogError("Received 'owner change' message type from '" + clientID + "' but there is no Network Behaviour associated with network ID: '" + networkID + "'.");
                        return;
                    }

                    if(BehaviourWasDestroyed(behaviourReference.networkBehaviour)) return;

                    if(behaviourReference.networkBehaviour.ownerID != clientID)
                    {
                        Debug.LogError("Received 'owner change' message type from '" + clientID + "' but they are not the owner of the Network Behaviour '" + behaviourReference.networkBehaviour + "' (Network ID: " + networkID + ").");
                        return;
                    }
                    if(newOwner != networkManager.serverID)
                    {
                        Debug.LogError("Received 'owner change' message type from '" + clientID + "' but they were trying to set the owner to someone other than the server which clients should only be able to return ownership to the server.");
                        return;
                    }

                    behaviourReference.ownerChangeDelegate.Invoke(newOwner, ownerCanUnspawn);


                }
                else
                {
                    if(!m_NetworkBehaviourDictionary.TryGetValue(networkID, out NetworkBehaviourReference behaviourReference))
                    {
                        Debug.LogError("Received 'owner change' message type from the server but there is no Network Behaviour associated with network ID: '" + networkID + "'.");
                        return;
                    }
                    if(BehaviourWasDestroyed(behaviourReference.networkBehaviour)) return;

                    behaviourReference.ownerChangeDelegate.Invoke(newOwner, ownerCanUnspawn);
                }
            }
        }

        #endregion Handle Messages

        ///<summary>
        /// Called when the client successfully connects Network Behaviours. Involves switching pending Network Behaviours to active and sending the server a success message.
        /// Client only.
        ///</summary>
        private void OnObjectConnectSuccess(NetworkBehaviourReference behaviourReference, ulong networkID, ulong ownerID, bool ownerCanUnspawn, bool destroyOnUnspawn)
        {
            m_NetworkBehaviours.Add(behaviourReference);
            m_NetworkBehaviourDictionary.Add(networkID, behaviourReference);

            //Send confirm connection
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);
                    //if(networkManager.enableLogging)
                        Debug.Log("Sending success of add object with Network ID: " + networkID);
                }
            
                MessageSender.Send(networkManager.serverID, objectSuccessMessageType, networkManager.networkInternalChannel, stream);
            }

            //Let our local behaviour know
            behaviourReference.connectedClientCallback.Invoke(networkID, ownerID, ownerCanUnspawn, destroyOnUnspawn);
        }


        private bool BehaviourWasDestroyed(NetworkBehaviour networkBehaviour)
        {
#if DEBUG
            if(ReferenceEquals(networkBehaviour, null))
            {
                //If this error occurs that means the target Network Behaviour is in fact null and not a valid reference to a destroyed Network Behaviour.
                //If this occurs that means that somewhere code is incorrectly setting the reference to null or a null check is not occuring when its supposed to. Report this issue.
                Debug.LogError("An internal issue occurred in the Network Behaviour Manager.");
                return true;
            }
#endif

            //The behaviour has not been destroyed, continue as normal
            if(networkBehaviour != null) return false;


            if(networkManager.isServer || (networkBehaviour.isOwner && networkBehaviour.ownerCanUnspawn)) //Server or allowed owners can destroy objects if they want but it's not the correct way of going about it because sending a message to clients will find out it is destroyed a little late.
            {
                Debug.LogWarning("Network Behaviour has been destroyed before it was Unspawned. It is recommended that the Network Behaviour is unspawned on the network before being destroyed. Unspawning now...");
                DoUnspawnOnNetwork(networkBehaviour);
                return true;
            }
            else
            {
                //If you get this error then alter your logic so that the client will not be destroying the Network Behaviour. Further errors will occur because of this and will cause game breaking issues on the client.
                //This issue can be partially bypassed by the server allowing this behaviour to be the owner and have the ownerCanUnspawn property enabled but the Network Behaviour should be unspawned before destroyed. Remember this should be a server authoritive game.
                throw new NotServerException("The client has destroyed a ready and connected Network Behaviour when they are not supposed to. Only the server will manage destroying the Network Behaviour.");
            }
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
        }

        private struct ReleasedNetworkID
        {
            public ulong networkID;
            public float releaseTime;
        }

        public struct NetworkBehaviourReference
        {
            public NetworkBehaviour networkBehaviour;
            public ServerBehaviourConnectedDelegate connectedServerCallback;
            public ClientBehaviourConnectedDelegate connectedClientCallback;
            public BehaviourDisconnectedDelegate disconnectedDelegate;
            public NetworkBehaviourRPCDelegate localRPCDelegate;
            public OwnerChangeDelegate ownerChangeDelegate;
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
