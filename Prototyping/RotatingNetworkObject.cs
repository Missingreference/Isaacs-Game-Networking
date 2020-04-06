using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Elanetic.Network;

using MLAPI.Serialization.Pooled;

public class RotatingNetworkObject : NetworkBehaviour
{
    public float rotateSpeed { get; set; } = 10.0f;
    public SpriteRenderer spriteRenderer { get; set; }


    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if(spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Square");
        }
        transform.position = new Vector3(0.0f, 1.9f, 0.0f);
        transform.localScale = new Vector3(1.35f, 1.0f, 1.0f);
    }

    protected override void OnNetworkReady(ulong clientID, Stream spawnPayload)
    {
        Debug.Log("On Network Start called");
    }

    protected override void OnNetworkShutdown()
    {
        Debug.Log("On Network Shutdown called");
    }

    protected override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        Debug.Log("Rotator OnGainedOwnership called");
    }

    protected override void OnLostOwnership()
    {
        base.OnLostOwnership();
        Debug.Log("Rotator OnLostOwnership called");
    }

    void Update()
    {
        if(isOwner && isServer)
        {
            //Do rotation
            transform.eulerAngles += new Vector3(0, 0, rotateSpeed * Time.deltaTime);
            using(PooledBitStream stream = PooledBitStream.Get())
            {
                using(PooledBitWriter writer = PooledBitWriter.Get(stream))
                {

                }
            }
        }
    }
}
