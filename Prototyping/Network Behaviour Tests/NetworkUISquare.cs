using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Isaac.Network;

public class NetworkUISquare : NetworkBehaviour
{
    private Image m_Image;

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

    void Update()
    {
        
    }
}
