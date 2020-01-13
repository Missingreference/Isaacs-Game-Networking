using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

using Isaac.Network;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;

public class NetworkUISquare : NetworkBehaviour
{
    public float rotateSpeed = 10.0f;

    private Image m_Image;

    //Settings
    private float m_FixedSendsPerSecond = 20.0f;
    private float m_MinRotationDifference = 1.5f;
    
    private float lastSendTime = 0.0f;
    private float lastSendRotation = 0.0f;

    void Awake()
    {
        //Add Image Component
        m_Image = gameObject.AddComponent<Image>();

        //Create texture
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(new Color[] { Color.black, Color.magenta, Color.magenta, Color.black });
        tex.Apply();

        //Apply sprite created from texture
        m_Image.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 4.0f);
    }

    void Start()
    {

    }

    void OnDestroy()
    {
        Debug.Log("Destroyed!!");
    }

    void Update()
    {
        if(isNetworkReady && isOwner)
        {
            transform.localEulerAngles += new Vector3(0,0,rotateSpeed * Time.deltaTime);
            SendRotation(transform.localEulerAngles.z);
        }
    }

    private void SendRotation(float newRotation)
    {
        if(networkManager.networkTime - lastSendTime >= (1.0f / m_FixedSendsPerSecond) && Mathf.Abs(transform.localEulerAngles.z - lastSendRotation) > m_MinRotationDifference)
        {
            lastSendTime = networkManager.networkTime;
            lastSendRotation = transform.localEulerAngles.z;
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                    writer.WriteSinglePacked(transform.localEulerAngles.z);

                if(isServer)
                    InvokeClientRPCAllExcept(ApplyRotation, ownerID, stream, "Rotate Channel"); //Debug.LogWarning("Not implemented");//InvokeClientRpcOnEveryoneExceptPerformance(ApplyTransform, OwnerClientId, stream, string.IsNullOrEmpty(Channel) ? "MLAPI_DEFAULT_MESSAGE" : Channel);
                else
                    InvokeServerRPC(SetRotation, stream, "Rotate Channel");
            }
        }
    }

    [ServerRPC(RequireOwnership=true)]
    private void SetRotation(ulong clientID, Stream stream)
    {
        using(PooledBitReader reader = PooledBitReader.Get(stream))
        {
            float rotation = reader.ReadSinglePacked();

            /* This is where we verify movement
            if(IsMoveValidDelegate != null && !IsMoveValidDelegate(lerpEndPos, new Vector3(xPos, yPos, zPos)))
            {
                //Invalid move!
                //TODO: Add rubber band (just a message telling them to go back)
                return;
            }*/

            using(PooledBitStream writeStream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(writeStream))
                {
                    writer.WriteSinglePacked(rotation);
                    /*
                    if(EnableRange)
                    {
                        for(int i = 0; i < NetworkingManager.Singleton.ConnectedClientsList.Count; i++)
                        {
                            if(!clientSendInfo.ContainsKey(NetworkingManager.Singleton.ConnectedClientsList[i].ClientId))
                            {
                                clientSendInfo.Add(NetworkingManager.Singleton.ConnectedClientsList[i].ClientId, new ClientSendInfo()
                                {
                                    clientId = NetworkingManager.Singleton.ConnectedClientsList[i].ClientId,
                                    lastMissedPosition = null,
                                    lastMissedRotation = null,
                                    lastSent = 0
                                });
                            }

                            ClientSendInfo info = clientSendInfo[NetworkingManager.Singleton.ConnectedClientsList[i].ClientId];
                            Vector3? receiverPosition = NetworkingManager.Singleton.ConnectedClientsList[i].PlayerObject == null ? null : new Vector3?(NetworkingManager.Singleton.ConnectedClientsList[i].PlayerObject.transform.position);
                            Vector3? senderPosition = NetworkingManager.Singleton.ConnectedClients[OwnerClientId].PlayerObject == null ? null : new Vector3?(NetworkingManager.Singleton.ConnectedClients[OwnerClientId].PlayerObject.transform.position);

                            if((receiverPosition == null || senderPosition == null && NetworkingManager.Singleton.NetworkTime - info.lastSent >= (1f / FixedSendsPerSecond)) || NetworkingManager.Singleton.NetworkTime - info.lastSent >= GetTimeForLerp(receiverPosition.Value, senderPosition.Value))
                            {
                                info.lastSent = NetworkingManager.Singleton.NetworkTime;
                                info.lastMissedPosition = null;
                                info.lastMissedRotation = null;

                                InvokeClientRpcOnClientPerformance(ApplyTransform, NetworkingManager.Singleton.ConnectedClientsList[i].ClientId, writeStream, string.IsNullOrEmpty(Channel) ? "MLAPI_DEFAULT_MESSAGE" : Channel);
                            }
                            else
                            {
                                info.lastMissedPosition = new Vector3(xPos, yPos, zPos);
                                info.lastMissedRotation = Quaternion.Euler(xRot, yRot, zRot);
                            }
                        }
                    }
                    else*/
                    {
                        InvokeClientRPCAllExcept(ApplyRotation, ownerID, writeStream, "Rotate Channel");
                        //InvokeClientRpcOnEveryoneExceptPerformance(ApplyTransform, OwnerClientId, writeStream, string.IsNullOrEmpty(Channel) ? "MLAPI_DEFAULT_MESSAGE" : Channel);
                    }
                }
            }
        }
    }

    [ClientRPC]
    private void ApplyRotation(ulong clientID, Stream stream)
    {
        using(PooledBitReader reader = PooledBitReader.Get(stream))
        {
            float rotation = reader.ReadSinglePacked();
            transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, rotation);
        }
    }

    protected override void OnNetworkReady(ulong clientID)
    {
        lastSendRotation = transform.eulerAngles.z;
        if(isServer)
        {
            if(clientID == networkManager.clientID)
            {
                networkManager.onClientConnect += OnClientConnected;
            }
            else
            {
                SetOwner(clientID);
            }
        }
    }

    protected override void OnNetworkShutdown()
    {
        if(isServer)
            networkManager.onClientConnect -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientID)
    {
        Debug.Log("Behaviour OnClientConnected called!");
    }

    protected override void OnGainedOwnership()
    {
        Debug.Log("Gained ownership!");
    }

    protected override void OnLostOwnership()
    {
        Debug.Log("Lost ownership!");
    }
}
