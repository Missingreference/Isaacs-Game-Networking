using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

using UnityEngine;

using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;

namespace Isaac.Network
{
    public partial class NetworkBehaviour : MonoBehaviour
    {

        internal readonly HashSet<ulong> observers = new HashSet<ulong>();

        /// <summary>
        /// Returns Observers enumerator
        /// </summary>
        /// <returns>Observers enumerator</returns>
        public HashSet<ulong>.Enumerator GetObservers()
        {
            throw new NotImplementedException();
            /*
            throw new NotImplementedException();

            return observers.GetEnumerator();
            */
        }

        /// <summary>
        /// Whether or not this object is visible to a specific client
        /// </summary>
        /// <param name="clientID">The clientId of the client</param>
        /// <returns>True if the client knows about the object</returns>
        public bool IsNetworkVisibleTo(ulong clientID)
        {
            throw new NotImplementedException();
            /*

            return observers.Contains(clientID);
            */
        }

        /// <summary>
        /// Shows a previously hidden object to a client
        /// </summary>
        /// <param name="clientID">The client to show the object to</param>
        /// <param name="payload">An optional payload to send as part of the spawn</param>
        public void NetworkShow(ulong clientID, Stream payload = null)
        {

            throw new NotImplementedException();
            /*

            if(!NetworkManager.Get().isServer)
            {
                //throw new NotServerException("Only server can change visibility");
                Debug.LogError("Only server can change visibility.");
                return;
            }

            if(observers.Contains(clientID))
            {
                //throw new VisibilityChangeException("The object is already visible");
                Debug.LogError("The object is already visible.");
                return;
            }

            // Send spawn call
            observers.Add(clientID);

            SpawnManager.SendSpawnCallForObject(clientID, this, payload);
            */
        }

        /// <summary>
        /// Shows a list of previously hidden objects to a client
        /// </summary>
        /// <param name="networkedObjects">The objects to show</param>
        /// <param name="clientID">The client to show the objects to</param>
        /// <param name="payload">An optional payload to send as part of the spawns</param>
        public static void NetworkShow(List<NetworkBehaviour> networkedObjects, ulong clientID, Stream payload = null)
        {
            throw new NotImplementedException();
            /*
            if(!NetworkManager.Get().isServer)
            {
                //throw new NotServerException("Only server can change visibility");
                Debug.LogError("Only server can change visibility.");
                return;
            }

            // Do the safety loop first to prevent putting the MLAPI in an invalid state.
            for(int i = 0; i < networkedObjects.Count; i++)
            {
                if(!false)//networkedObjects[i].IsSpawned)
                {
                    //throw new SpawnStateException("Object is not spawned");
                    Debug.LogError("Object is not spawned.");
                    return;
                }

                if(networkedObjects[i].observers.Contains(clientID))
                {
                    //throw new VisibilityChangeException("NetworkedObject with NetworkId: " + networkedObjects[i].NetworkId + " is already visible");
                    Debug.LogError("NetworkedObject with NetworkId: " + networkedObjects[i].networkID + " is already visible");
                    return;
                }
            }

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt16Packed((ushort)networkedObjects.Count);
                }

                for(int i = 0; i < networkedObjects.Count; i++)
                {
                    // Send spawn call
                    networkedObjects[i].observers.Add(clientID);

                    SpawnManager.WriteSpawnCallForObject(stream, clientID, networkedObjects[i], payload);
                }

                //InternalMessageSender.SendToAll(MessageType.NETWORK_ADD_OBJECTS, "NETWORK_INTERNAL", stream);
            }*/
        }

        /// <summary>
        /// Hides a object from a specific client
        /// </summary>
        /// <param name="clientID">The client to hide the object for</param>
        public void NetworkHide(ulong clientID)
        {
            throw new NotImplementedException();
            /*
            if(!NetworkManager.Get().isServer)
            {
                //throw new NotServerException("Only server can change visibility");
                Debug.LogError("Only server can change visibility.");
                return;
            }

            if(!observers.Contains(clientID))
            {
                //throw new VisibilityChangeException("The object is already hidden");
                Debug.LogError("The object is already hidden.");
                return;
            }

            if(clientID == NetworkManager.Get().serverID)
            {
                //throw new VisibilityChangeException("Cannot hide an object from the server");
                Debug.LogError("Cannot hide an object from the server.");
                return;
            }


            // Send destroy call
            observers.Remove(clientID);

            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt64Packed(networkID);

                    //InternalMessageSender.SendToAll(MessageType.NETWORK_DESTROY_OBJECT, "NETWORK_INTERNAL", stream);
                }
            }
            */
        }

        /// <summary>
        /// Hides a list of objects from a client
        /// </summary>
        /// <param name="networkedObjects">The objects to hide</param>
        /// <param name="clientID">The client to hide the objects from</param>
        public static void NetworkHide(List<NetworkBehaviour> networkedObjects, ulong clientID)
        {
            throw new NotImplementedException();
            /*
            if(!NetworkManager.Get().isServer)
            {
                //throw new NotServerException("Only server can change visibility");
                Debug.LogError("Only the server can change visibility.");
                return;
            }

            if(clientID == NetworkManager.Get().serverID)
            {
                //throw new VisibilityChangeException("Cannot hide an object from the server");
                Debug.LogError("Cannot hide an object from the server.");
                return;
            }

            // Do the safety loop first to prevent putting the MLAPI in an invalid state.
            for(int i = 0; i < networkedObjects.Count; i++)
            {
                if(!true)//networkedObjects[i].IsSpawned)
                {
                    //throw new SpawnStateException("Object is not spawned");
                    Debug.LogError("Object is not spawned.");
                    return;
                }

                if(!networkedObjects[i].observers.Contains(clientID))
                {
                    //throw new VisibilityChangeException("NetworkedObject with NetworkId: " + networkedObjects[i].NetworkId + " is already hidden");
                    Debug.LogError("NetworkedObject with NetworkId: " + networkedObjects[i].networkID + " is already hidden");
                    return;
                }
            }


            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteUInt16Packed((ushort)networkedObjects.Count);

                    for(int i = 0; i < networkedObjects.Count; i++)
                    {
                        // Send destroy call
                        networkedObjects[i].observers.Remove(clientID);

                        writer.WriteUInt64Packed(networkedObjects[i].networkID);
                    }
                }

                //InternalMessageSender.SendToAll(MessageType.NETWORK_DESTROY_OBJECTS, "NETWORK_INTERNAL", stream);
            }
            */
        }
    }
}