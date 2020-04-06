using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using MLAPI.Hashing;
using MLAPI.Internal;
using MLAPI.Messaging;
using MLAPI.Security;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;

using Elanetic.Network;
using Elanetic.Network.Messaging;

namespace MLAPI.Spawning
{
    /// <summary>
    /// Class that handles object spawning
    /// </summary>
    public static class SpawnManager
    {
        /// <summary>
        /// The currently spawned objects
        /// </summary>
        public static readonly Dictionary<ulong, NetworkBehaviour> SpawnedObjects = new Dictionary<ulong, NetworkBehaviour>();
        // Pending SoftSync objects
        internal static readonly Dictionary<ulong, NetworkBehaviour> pendingSoftSyncObjects = new Dictionary<ulong, NetworkBehaviour>();
        /// <summary>
        /// A list of the spawned objects
        /// </summary>
        public static readonly List<NetworkBehaviour> SpawnedObjectsList = new List<NetworkBehaviour>();
        /// <summary>
        /// The delegate used when spawning a networked object
        /// </summary>
        /// <param name="position">The position to spawn the object at</param>
        /// <param name="rotation">The rotation to spawn the object with</param>
        public delegate NetworkBehaviour SpawnHandlerDelegate(Vector3 position, Quaternion rotation);
        /// <summary>
        /// The delegate used when destroying networked objects
        /// </summary>
        /// <param name="networkedBehaviour">The network object to be destroy</param>
        public delegate void DestroyHandlerDelegate(NetworkBehaviour networkedBehaviour);

        internal static readonly Dictionary<ulong, SpawnHandlerDelegate> customSpawnHandlers = new Dictionary<ulong, SpawnHandlerDelegate>();
        internal static readonly Dictionary<ulong, DestroyHandlerDelegate> customDestroyHandlers = new Dictionary<ulong, DestroyHandlerDelegate>();

        /// <summary>
        /// Registers a delegate for spawning networked prefabs, useful for object pooling
        /// </summary>
        /// <param name="prefabHash">The prefab hash to spawn</param>
        /// <param name="handler">The delegate handler</param>
        public static void RegisterSpawnHandler(ulong prefabHash, SpawnHandlerDelegate handler)
        {
            if(customSpawnHandlers.ContainsKey(prefabHash))
            {
                customSpawnHandlers[prefabHash] = handler;
            }
            else
            {
                customSpawnHandlers.Add(prefabHash, handler);
            }
        }

        /// <summary>
        /// Registers a delegate for destroying networked objects, useful for object pooling
        /// </summary>
        /// <param name="prefabHash">The prefab hash to destroy</param>
        /// <param name="handler">The delegate handler</param>
        public static void RegisterCustomDestroyHandler(ulong prefabHash, DestroyHandlerDelegate handler)
        {
            if(customDestroyHandlers.ContainsKey(prefabHash))
            {
                customDestroyHandlers[prefabHash] = handler;
            }
            else
            {
                customDestroyHandlers.Add(prefabHash, handler);
            }
        }

        /// <summary>
        /// Removes the custom spawn handler for a specific prefab hash
        /// </summary>
        /// <param name="prefabHash">The prefab hash of the prefab spawn handler that is to be removed</param>
        public static void RemoveCustomSpawnHandler(ulong prefabHash)
        {
            customSpawnHandlers.Remove(prefabHash);
        }

        /// <summary>
        /// Removes the custom destroy handler for a specific prefab hash
        /// </summary>
        /// <param name="prefabHash">The prefab hash of the prefab destroy handler that is to be removed</param>
        public static void RemoveCustomDestroyHandler(ulong prefabHash)
        {
            customDestroyHandlers.Remove(prefabHash);
        }

        internal static readonly Queue<ReleasedNetworkId> releasedNetworkObjectIds = new Queue<ReleasedNetworkId>();
        private static ulong networkObjectIdCounter;
        internal static ulong GetNetworkObjectId()
        {
            if(releasedNetworkObjectIds.Count > 0 && NetworkManager.Get().config.recycleNetworkIDs && (Time.unscaledTime - releasedNetworkObjectIds.Peek().ReleaseTime) >= NetworkManager.Get().config.networkIDRecycleDelay)
            {
                return releasedNetworkObjectIds.Dequeue().NetworkID;
            }
            else
            {
                networkObjectIdCounter++;
                return networkObjectIdCounter;
            }
        }

        /// <summary>
        /// Gets the prefab index of a given prefab hash
        /// </summary>
        /// <param name="hash">The hash of the prefab</param>
        /// <returns>The index of the prefab</returns>
        public static int GetNetworkedPrefabIndexOfHash(ulong hash)
        {
            /*for (int i = 0; i < m_NetworkManager.config.NetworkedPrefabs.Count; i++)
			{
				if (NetworkManager.Get().NetworkConfig.NetworkedPrefabs[i].Hash == hash)
					return i;
			}
			*/
            return -1;
        }

        /// <summary>
        /// Returns the prefab hash for the networked prefab with a given index
        /// </summary>
        /// <param name="index">The networked prefab index</param>
        /// <returns>The prefab hash for the given prefab index</returns>
        public static ulong GetPrefabHashFromIndex(int index)
        {
            Debug.LogError("Using unsupported method.");
            return 0;//m_NetworkManager.config.NetworkedPrefabs[index].Hash;
        }

        /// <summary>
        /// Returns the prefab hash for a given prefab hash generator
        /// </summary>
        /// <param name="generator">The prefab hash generator</param>
        /// <returns>The hash for the given generator</returns>
        public static ulong GetPrefabHashFromGenerator(string generator)
        {
            return generator.GetStableHash64();
        }

        internal static void RemoveOwnership(NetworkBehaviour netObject)
        {
            /*
			if (!m_NetworkManager.isServer)
			{
				Debug.LogError("Only the server can change ownership");
				//throw new NotServerException("Only the server can change ownership");
				return;
			}

			if (!netObject.IsSpawned)
			{
				Debug.LogError("Object is not spawned");
				//throw new SpawnStateException("Object is not spawned");
				return;
			}

			for (int i = m_NetworkManager.ConnectedClients[netObject.ownerClientID].ownedObjects.Count - 1; i > -1; i--)
			{
				if (m_NetworkManager.ConnectedClients[netObject.ownerClientID].ownedObjects[i] == netObject)
					m_NetworkManager.ConnectedClients[netObject.ownerClientID].ownedObjects.RemoveAt(i);
			}

			netObject._ownerClientId = null;

			using (PooledBitStream stream = PooledBitStream.Get())
			{
				using (PooledBitWriter writer = PooledBitWriter.Get(stream))
				{
					writer.WriteUInt64Packed(netObject.NetworkId);
					writer.WriteUInt64Packed(netObject.OwnerClientId);

					InternalMessageSender.Send(MLAPIConstants.MLAPI_CHANGE_OWNER, "MLAPI_INTERNAL", stream, SecuritySendFlags.None, netObject);
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void ChangeOwnership(NetworkBehaviour netObject, ulong clientId)
        {
            /*
			if (!m_NetworkManager.isServer)
			{
				Debug.LogError("Only the server can change ownership");
				return;
			}

			//TODO isSpawned?
			//if (!netObject.isSpawned)
			{
				//throw new SpawnStateException("Object is not spawned");
			}

			for (int i = m_NetworkManager.ConnectedClients[netObject.ownerClientID].ownedObjects.Count - 1; i > -1; i--)
			{
				if (m_NetworkManager.ConnectedClients[netObject.ownerClientID].ownedObjects[i] == netObject)
					m_NetworkManager.ConnectedClients[netObject.ownerClientID].ownedObjects.RemoveAt(i);
			}

			m_NetworkManager.ConnectedClients[clientId].ownedObjects.Add(netObject);
			netObject.ownerClientID = clientId;

			using (PooledBitStream stream = PooledBitStream.Get())
			{
				using (PooledBitWriter writer = PooledBitWriter.Get(stream))
				{
					writer.WriteUInt64Packed(netObject.networkID);
					writer.WriteUInt64Packed(clientId);

					InternalMessageSender.Send(MLAPIConstants.MLAPI_CHANGE_OWNER, "MLAPI_INTERNAL", stream, SecuritySendFlags.None, netObject);
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        // Only ran on Client
        internal static NetworkBehaviour CreateLocalNetworkedObject(bool softCreate, ulong instanceId, ulong prefabHash, ulong? parentNetworkId, Vector3? position, Quaternion? rotation)
        {
            /*
			NetworkBehaviour parent = null;

			if (parentNetworkId != null && SpawnedObjects.ContainsKey(parentNetworkId.Value))
			{
				parent = SpawnedObjects[parentNetworkId.Value];
			}
			else if (parentNetworkId != null)
			{
				Debug.LogWarning("Cannot find parent. Parent objects always have to be spawned and replicated BEFORE the child");
			}
			
			if(!false)//if (!NetworkManager.Get().config.EnableSceneManagement || false NetworkManager.Get().config.UsePrefabSync || !softCreate)
			{
				// Create the object
				if (customSpawnHandlers.ContainsKey(prefabHash))
				{
					NetworkBehaviour networkedObject = customSpawnHandlers[prefabHash](position.GetValueOrDefault(Vector3.zero), rotation.GetValueOrDefault(Quaternion.identity));

					if (parent != null)
					{
						networkedObject.transform.SetParent(parent.transform, true);
					}

					if (NetworkSceneManager.isSpawnedObjectsPendingInDontDestroyOnLoad)
					{
						GameObject.DontDestroyOnLoad(networkedObject.gameObject);
					}

					return networkedObject;
				}
				else
				{
					GameObject prefab = NetworkManager.Get().config.NetworkedPrefabs[GetNetworkedPrefabIndexOfHash(prefabHash)].Prefab;

					NetworkBehaviour networkedObject = ((position == null && rotation == null) ? MonoBehaviour.Instantiate(prefab) : MonoBehaviour.Instantiate(prefab, position.GetValueOrDefault(Vector3.zero), rotation.GetValueOrDefault(Quaternion.identity))).GetComponent<NetworkBehaviour>();

					if (parent != null)
					{
						networkedObject.transform.SetParent(parent.transform, true);
					}

					if (NetworkSceneManager.isSpawnedObjectsPendingInDontDestroyOnLoad)
					{
						GameObject.DontDestroyOnLoad(networkedObject.gameObject);
					}

					return networkedObject;
				}
			}
			else
			{
				// SoftSync them by mapping
				if (!pendingSoftSyncObjects.ContainsKey(instanceId))
				{
					// TODO(TODO created by MLAPI and not created by Isaac): Fix this message
					Debug.LogError("Cannot find pending soft sync object. Is the projects the same?");
					return null;
				}

				NetworkBehaviour networkedObject = pendingSoftSyncObjects[instanceId];
				pendingSoftSyncObjects.Remove(instanceId);

				if (parent != null)
				{
					networkedObject.transform.SetParent(parent.transform, true);
				}

				return networkedObject;
			}*/

            Debug.LogWarning("Using unimplemented function");
            return null;
        }

        // Ran on both server and client
        internal static void SpawnNetworkedObjectLocally(NetworkBehaviour netObject, ulong networkId, bool sceneObject, bool playerObject, ulong? ownerClientId, Stream dataStream, bool readPayload, int payloadLength, bool readNetworkedVar, bool destroyWithScene)
        {
            /*
			if (netObject == null)
			{
				throw new ArgumentNullException(nameof(netObject), "Cannot spawn null object");
			}

			if (netObject.IsSpawned)
			{
				throw new SpawnStateException("Object is already spawned");
			}


			if (readNetworkedVar && NetworkManager.Get().config.EnableNetworkedVar) netObject.SetNetworkedVarData(dataStream);

			netObject.IsSpawned = true;

			netObject.IsSceneObject = sceneObject;
			netObject.networkID = networkId;

			netObject.DestroyWithScene = sceneObject || destroyWithScene;

			netObject._ownerClientId = ownerClientId;
			netObject.IsPlayerObject = playerObject;

			SpawnedObjects.Add(netObject.networkID, netObject);
			SpawnedObjectsList.Add(netObject);

			if (ownerClientId != null)
			{
				if (NetworkManager.Get().isServer)
				{
					if (playerObject)
					{
						NetworkManager.Get().ConnectedClients[ownerClientId.Value].PlayerObject = netObject;
					}
					else
					{
						NetworkManager.Get().ConnectedClients[ownerClientId.Value].OwnedObjects.Add(netObject);
					}
				}
				else if (playerObject && ownerClientId.Value == NetworkManager.Get().clientID)
				{
					NetworkManager.Get().ConnectedClients[ownerClientId.Value].PlayerObject = netObject;
				}
			}

			if (NetworkManager.Get().isServer)
			{
				for (int i = 0; i < NetworkManager.Get().ConnectedClientsList.Count; i++)
				{
					if (netObject.CheckObjectVisibility == null || netObject.CheckObjectVisibility(NetworkManager.Get().ConnectedClientsList[i].ClientId))
					{
						netObject.observers.Add(NetworkManager.Get().ConnectedClientsList[i].ClientId);
					}
				}
			}

			netObject.ResetNetworkedStartInvoked();

			if (readPayload)
			{
				using (PooledBitStream payloadStream = PooledBitStream.Get())
				{
					payloadStream.CopyUnreadFrom(dataStream, payloadLength);
					dataStream.Position += payloadLength;
					payloadStream.Position = 0;
					netObject.InvokeBehaviourNetworkSpawn(payloadStream);
				}
			}
			else
			{
				netObject.InvokeBehaviourNetworkSpawn(null);
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void SendSpawnCallForObject(ulong clientID, NetworkBehaviour netObject, Stream payload)
        {
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                WriteSpawnCallForObject(stream, clientID, netObject, payload);

                //InternalMessageSender.Send(clientID, MessageType.NETWORK_ADD_OBJECT, "MLAPI_INTERNAL", stream);
            }
        }

        internal static void WriteSpawnCallForObject(Serialization.BitStream stream, ulong clientID, NetworkBehaviour netObject, Stream payload)
        {
            /*
			using (PooledBitWriter writer = PooledBitWriter.Get(stream))
			{
				writer.WriteBool(netObject.IsPlayerObject);
				writer.WriteUInt64Packed(netObject.networkID);
				writer.WriteUInt64Packed(netObject.ownerClientID);

				NetworkBehaviour parent = null;

				if (!netObject.AlwaysReplicateAsRoot && netObject.transform.parent != null)
				{
					parent = netObject.transform.parent.GetComponent<NetworkBehaviour>();
				}

				if (parent == null)
				{
					writer.WriteBool(false);
				}
				else
				{
					writer.WriteBool(true);
					writer.WriteUInt64Packed(parent.networkID);
				}

				if(!false)//if (!NetworkManager.Get().NetworkConfig.EnableSceneManagement || NetworkManager.Get().NetworkConfig.UsePrefabSync)
				{
					writer.WriteUInt64Packed(netObject.PrefabHash);
				}
				else
				{
					writer.WriteBool(netObject.IsSceneObject == null ? true : netObject.IsSceneObject.Value);

					if (netObject.IsSceneObject == null || netObject.IsSceneObject.Value)
					{
						writer.WriteUInt64Packed(netObject.NetworkedInstanceId);
					}
					else
					{
						writer.WriteUInt64Packed(netObject.PrefabHash);
					}
				}

				if (netObject.IncludeTransformWhenSpawning == null || netObject.IncludeTransformWhenSpawning(clientId))
				{
					writer.WriteBool(true);
					writer.WriteSinglePacked(netObject.transform.position.x);
					writer.WriteSinglePacked(netObject.transform.position.y);
					writer.WriteSinglePacked(netObject.transform.position.z);

					writer.WriteSinglePacked(netObject.transform.rotation.eulerAngles.x);
					writer.WriteSinglePacked(netObject.transform.rotation.eulerAngles.y);
					writer.WriteSinglePacked(netObject.transform.rotation.eulerAngles.z);
				}
				else
				{
					writer.WriteBool(false);
				}

				writer.WriteBool(payload != null);

				if (payload != null)
				{
					writer.WriteInt32Packed((int)payload.Length);
				}

				if (NetworkManager.Get().config.EnableNetworkedVar)
				{
					netObject.WriteNetworkedVarData(stream, clientId);
				}

				if (payload != null) stream.CopyFrom(payload);
			}
			*/

            Debug.LogWarning("Using unimplemented function");
        }

        internal static void UnSpawnObject(NetworkBehaviour netObject)
        {
            /*
			if (!netObject.IsSpawned)
			{
				throw new SpawnStateException("Object is not spawned");
			}

			if (!NetworkManager.Get().isServer)
			{
				throw new NotServerException("Only server unspawn objects");
			}

			OnDestroyObject(netObject.networkID, false);
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        // Makes scene objects ready to be reused
        internal static void ServerResetShudownStateForSceneObjects()
        {
            /*
			for (int i = 0; i < SpawnedObjectsList.Count; i++)
			{
				if ((SpawnedObjectsList[i].IsSceneObject != null && SpawnedObjectsList[i].IsSceneObject == true) || SpawnedObjectsList[i].DestroyWithScene)
				{
					SpawnedObjectsList[i].IsSpawned = false;
					SpawnedObjectsList[i].DestroyWithScene = false;
					SpawnedObjectsList[i].IsSceneObject = null;
				}
			}
			*/

            Debug.LogWarning("Using unimplemented function");
        }

        internal static void ServerDestroySpawnedSceneObjects()
        {
            /*
			for (int i = 0; i < SpawnedObjectsList.Count; i++)
			{
				if ((SpawnedObjectsList[i].IsSceneObject != null && SpawnedObjectsList[i].IsSceneObject == true) || SpawnedObjectsList[i].DestroyWithScene)
				{
					if (customDestroyHandlers.ContainsKey(SpawnedObjectsList[i].PrefabHash))
					{
						customDestroyHandlers[SpawnedObjectsList[i].PrefabHash](SpawnedObjectsList[i]);
						SpawnManager.OnDestroyObject(SpawnedObjectsList[i].networkID, false);
					}
					else
					{
						MonoBehaviour.Destroy(SpawnedObjectsList[i].gameObject);
					}
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void DestroyNonSceneObjects()
        {
            /*
			NetworkBehaviour[] netObjects = MonoBehaviour.FindObjectsOfType<NetworkBehaviour>();

			for (int i = 0; i < netObjects.Length; i++)
			{
				if (netObjects[i].IsSceneObject != null && netObjects[i].IsSceneObject.Value == false)
				{
					if (customDestroyHandlers.ContainsKey(netObjects[i].PrefabHash))
					{
						customDestroyHandlers[netObjects[i].PrefabHash](netObjects[i]);
						SpawnManager.OnDestroyObject(netObjects[i].networkID, false);
					}
					else
					{
						MonoBehaviour.Destroy(netObjects[i].gameObject);
					}
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void DestroySceneObjects()
        {
            /*
			NetworkBehaviour[] netObjects = MonoBehaviour.FindObjectsOfType<NetworkBehaviour>();

			for (int i = 0; i < netObjects.Length; i++)
			{
				if (netObjects[i].IsSceneObject == null || netObjects[i].IsSceneObject.Value == true)
				{
					if (customDestroyHandlers.ContainsKey(netObjects[i].PrefabHash))
					{
						customDestroyHandlers[netObjects[i].PrefabHash](netObjects[i]);
						SpawnManager.OnDestroyObject(netObjects[i].networkID, false);
					}
					else
					{
						MonoBehaviour.Destroy(netObjects[i].gameObject);
					}
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void ServerSpawnSceneObjectsOnStartSweep()
        {
            /*
			NetworkBehaviour[] networkedObjects = MonoBehaviour.FindObjectsOfType<NetworkBehaviour>();

			for (int i = 0; i < networkedObjects.Length; i++)
			{
				if (networkedObjects[i].IsSceneObject == null)
				{
					SpawnNetworkedObjectLocally(networkedObjects[i], GetNetworkObjectId(), true, false, null, null, false, 0, false, true);
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void ClientCollectSoftSyncSceneObjectSweep(NetworkBehaviour[] networkedObjects)
        {
            /*
			if (networkedObjects == null)
				networkedObjects = MonoBehaviour.FindObjectsOfType<NetworkBehaviour>();

			for (int i = 0; i < networkedObjects.Length; i++)
			{
				if (networkedObjects[i].IsSceneObject == null)
				{
					pendingSoftSyncObjects.Add(networkedObjects[i].NetworkedInstanceId, networkedObjects[i]);
				}
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }

        internal static void OnDestroyObject(ulong networkId, bool destroyGameObject)
        {
            /*
			if (NetworkManager.Get() == null)
				return;

			//Removal of spawned object
			if (!SpawnedObjects.ContainsKey(networkId))
				return;

			if (!SpawnedObjects[networkId].IsOwnedByServer && !SpawnedObjects[networkId].IsPlayerObject &&
				NetworkManager.Get().ConnectedClients.ContainsKey(SpawnedObjects[networkId].ownerClientID))
			{
				//Someone owns it.
				for (int i = NetworkManager.Get().ConnectedClients[SpawnedObjects[networkId].ownerClientID].OwnedObjects.Count - 1; i > -1; i--)
				{
					if (NetworkManager.Get().ConnectedClients[SpawnedObjects[networkId].ownerClientID].OwnedObjects[i].NetworkId == networkId)
						NetworkManager.Get().ConnectedClients[SpawnedObjects[networkId].ownerClientID].OwnedObjects.RemoveAt(i);
				}
			}
			SpawnedObjects[networkId].IsSpawned = false;

			if (NetworkManager.Get() != null && NetworkManager.Get().isServer)
			{
				if (NetworkManager.Get().config.RecycleNetworkIds)
				{
					releasedNetworkObjectIds.Enqueue(new ReleasedNetworkId()
					{
						NetworkId = networkId,
						ReleaseTime = Time.unscaledTime
					});
				}

				if (SpawnedObjects[networkId] != null)
				{
					using (PooledBitStream stream = PooledBitStream.Get())
					{
						using (PooledBitWriter writer = PooledBitWriter.Get(stream))
						{
							writer.WriteUInt64Packed(networkId);

							InternalMessageSender.Send(MLAPIConstants.MLAPI_DESTROY_OBJECT, "MLAPI_INTERNAL", stream, SecuritySendFlags.None, SpawnedObjects[networkId]);
						}
					}
				}
			}

			GameObject go = SpawnedObjects[networkId].gameObject;

			if (destroyGameObject && go != null)
			{
				if (customDestroyHandlers.ContainsKey(SpawnedObjects[networkId].PrefabHash))
				{
					customDestroyHandlers[SpawnedObjects[networkId].PrefabHash](SpawnedObjects[networkId]);
					SpawnManager.OnDestroyObject(networkId, false);
				}
				else
				{
					MonoBehaviour.Destroy(go);
				}
			}

			SpawnedObjects.Remove(networkId);

			for (int i = SpawnedObjectsList.Count - 1; i > -1; i--)
			{
				if (SpawnedObjectsList[i].networkID == networkId)
					SpawnedObjectsList.RemoveAt(i);
			}
			*/


            Debug.LogWarning("Using unimplemented function");
        }
    }
    internal struct ReleasedNetworkId
    {
        public ulong NetworkID;
        public float ReleaseTime;
    }
}