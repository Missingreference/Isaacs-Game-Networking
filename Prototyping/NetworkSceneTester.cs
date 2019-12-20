using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Isaac.Network;
using Isaac.Network.SceneManagement;
using UnityEngine.SceneManagement;

public class NetworkSceneTester : MonoBehaviour
{
    NetworkSceneManager networkSceneManager;

    void Awake()
    {

    }

    void Start()
    {
        NetworkManager.Get().onInitialize += OnNetworkInit;
    }


    void OnNetworkInit()
    {
        networkSceneManager = NetworkManager.Get().GetModule<NetworkSceneManager>();
        networkSceneManager.onClientSceneLoad += OnClientSceneLoad;
        networkSceneManager.onClientSceneUnload += OnClientSceneUnload;
        networkSceneManager.onServerSceneLoad += OnServerSceneLoad;
        networkSceneManager.onServerSceneUnload += OnServerSceneUnload;
    }

    void OnClientSceneLoad(ulong clientID, string sceneName, bool isAdditive)
    {
        Debug.Log("CLIENT LOADED A SCENE: Client ID: " + clientID.ToString() + " | Scene: " + sceneName + " | isAdditive: " + isAdditive.ToString());
    }

    void OnClientSceneUnload(ulong clientID, string sceneName)
    {
        Debug.Log("CLIENT UNLOADED A SCENE: Client ID: " + clientID.ToString() + " | Scene: " + sceneName);
    }

    void OnServerSceneLoad(string sceneName, bool isAdditive)
    {
        Debug.Log("SERVER LOADED A SCENE: Scene: " + sceneName + " | isAdditive: " + isAdditive.ToString());
    }

    void OnServerSceneUnload(string sceneName)
    {
        Debug.Log("SERVER LOADED A SCENE: Scene: " + sceneName);
    }
}
