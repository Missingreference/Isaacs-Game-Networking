using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using MLAPI.Serialization.Pooled;
using MLAPI.Messaging;
using MLAPI.Hashing;

using Isaac.Network;
using Isaac.Network.Messaging;

namespace Isaac.Network.SceneManagement
{
    public class NetworkSceneManager : NetworkModule
    {
        public byte sceneChangeMessageType { get; private set; } = 255;

        public override Type[] dependencies => new Type[] { typeof(NetworkMessageHandler) };

        /// <summary>
        /// Called on the server when a client finishes loading a scene. Scene parameter CAN be null/empty if the client loads a scene that server does not recognize.
        /// (sendingClientID, sceneName, isAdditive)
        /// </summary>
        public Action<ulong, string, bool> onClientSceneLoad;
        /// <summary>
        /// Called on the server when a client finishes unloading a scene. Scene parameter CAN be null/empty if the client loads a scene that server does not recognize.
        /// (sendingClientID, sceneName)
        /// </summary>
        public Action<ulong, string> onClientSceneUnload;
        /// <summary>
        /// Called on the client when the server finishes loading a scene. Scene parameter CAN be null/empty if the client loads a scene that client does not recognize.
        /// (sceneName, isAdditive)
        /// </summary>
        public Action<string, bool> onServerSceneLoad;
        /// <summary>
        /// Called on the client when the server finishes loading a scene. Scene parameter CAN be null/empty if the server loads a scene that client does not recognize.
        /// (sceneName)
        /// </summary>
        public Action<string> onServerSceneUnload;

        public Dictionary<ulong, string> sceneHashes = new Dictionary<ulong, string>();

        private NetworkMessageHandler m_MessageHandler;

        public NetworkSceneManager()
        {

        }

        public override void Init(NetworkModule[] loadedDependencies)
        {
            sceneHashes.Clear();
            for(int i = 0; i < GetAllSceneNames().Length; i++)
            {
                sceneHashes.Add(GetAllSceneNames()[i].GetStableHash(networkManager.config.rpcHashSize), GetAllSceneNames()[i]);
            }

            if(m_MessageHandler == null)
            {
                for(int i = 0; i < loadedDependencies.Length; i++)
                {
                    if(loadedDependencies[i] is NetworkMessageHandler)
                    {
                        m_MessageHandler = loadedDependencies[i] as NetworkMessageHandler;
                    }
                }

                sceneChangeMessageType = m_MessageHandler.RegisterMessageType("SCENE_CHANGE", HandleSceneChangeMessage, NetworkMessageHandler.NetworkMessageReceiver.Both);
            }
            SceneManager.sceneLoaded += OnSceneLoad;
            SceneManager.sceneUnloaded += OnSceneUnload;
        }

        public override void Shutdown()
        {
            SceneManager.sceneLoaded -= OnSceneLoad;
            SceneManager.sceneUnloaded -= OnSceneUnload;
        }

        //Unity SceneManager load event callback
        private void OnSceneLoad(Scene scene, LoadSceneMode loadSceneMode)
        {
            if(networkManager.isServer)
            {
                if(!networkManager.config.serverSendSceneEvents)
                    return;

                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        writer.WriteBool(true); //Is Loading
                        writer.WriteBool(loadSceneMode == LoadSceneMode.Additive); //Is Additive?
                        writer.WriteUInt64Packed(scene.name.GetStableHash(NetworkManager.Get().config.rpcHashSize));

                        MessageSender.SendToAll(sceneChangeMessageType, networkManager.networkInternalChannel, stream);
                    }
                }
            }
            else if(networkManager.isClient)
            {
                if(!networkManager.config.clientSendSceneEvents)
                    return;

                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        writer.WriteBool(true); //Is Loading
                        writer.WriteBool(loadSceneMode == LoadSceneMode.Additive); //Is Additive?
                        writer.WriteUInt64Packed(scene.name.GetStableHash(NetworkManager.Get().config.rpcHashSize));

                        MessageSender.Send(networkManager.serverID, sceneChangeMessageType, networkManager.networkInternalChannel, stream);
                    }
                }
            }
        }

        //Unity SceneManager unload event callback
        private void OnSceneUnload(Scene scene)
        {
            if(networkManager.isServer)
            {
                if(!networkManager.config.serverSendSceneEvents)
                    return;

                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        writer.WriteBool(false); //Is Loading
                        writer.WriteUInt64Packed(scene.name.GetStableHash(NetworkManager.Get().config.rpcHashSize));

                        MessageSender.SendToAll(sceneChangeMessageType, networkManager.networkInternalChannel, stream);
                    }
                }
            }
            else if(networkManager.isClient)
            {
                if(!networkManager.config.clientSendSceneEvents)
                    return;

                using(PooledBitStream stream = PooledBitStream.Get())
                {
                    using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    {
                        writer.WriteBool(false); //Is Loading
                        writer.WriteUInt64Packed(scene.name.GetStableHash(NetworkManager.Get().config.rpcHashSize));

                        MessageSender.Send(networkManager.serverID, sceneChangeMessageType, networkManager.networkInternalChannel, stream);
                    }
                }
            }
        }

        //Network SCENE_CHANGE message callback
        private void HandleSceneChangeMessage(ulong sendingClientID, Stream stream, float receiveTime)
        {
            using(PooledBitReader reader = PooledBitReader.Get(stream))
            {
                bool isLoading = reader.ReadBool();
                if(isLoading) //Is Loading
                {
                    //Load
                    bool isAdditive = reader.ReadBool();
                    sceneHashes.TryGetValue(reader.ReadUInt64Packed(), out string sceneName);
                    if(networkManager.isServer)
                    {
                        onClientSceneLoad?.Invoke(sendingClientID, sceneName, isAdditive);
                    }
                    else if(networkManager.isClient)
                    {
                        onServerSceneLoad?.Invoke(sceneName, isAdditive);
                    }
                }
                else
                {
                    //Unload
                    sceneHashes.TryGetValue(reader.ReadUInt64Packed(), out string sceneName);
                    if(networkManager.isServer)
                    {
                        onClientSceneUnload?.Invoke(sendingClientID, sceneName);
                    }
                    else if(networkManager.isClient)
                    {
                        onServerSceneUnload?.Invoke(sceneName);
                    }
                }
            }
        }

        string[] m_AllSceneNames;
        private string[] GetAllSceneNames()
        {
            if(m_AllSceneNames == null)
            {
                m_AllSceneNames = new string[UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings];
                for(int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
                {
                    m_AllSceneNames[i] = System.IO.Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                }
            }

            return m_AllSceneNames;
        }
    }
}