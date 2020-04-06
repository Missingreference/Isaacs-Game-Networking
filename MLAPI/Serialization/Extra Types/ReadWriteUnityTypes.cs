using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using MLAPI.Serialization;

namespace Elanetic.Network.Serialization
{
    public static class ReadWriteUnityTypes
    {
        public static void WriteTexture2D(this BitWriter writer, Texture2D texture2D)
        {
            if(texture2D == null) throw new ArgumentNullException(nameof(texture2D));
            writer.WriteInt32(texture2D.width);
            writer.WriteInt32(texture2D.height);
            writer.WriteInt32((int)texture2D.format);

            writer.WriteByteArray(texture2D.GetRawTextureData());
        }

        public static Texture2D ReadTexture2D(this BitReader reader)
        {
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            TextureFormat format = (TextureFormat)reader.ReadInt32();

            Texture2D texture2D = new Texture2D(width, height, format, false);

            texture2D.LoadRawTextureData(reader.ReadByteArray());
            texture2D.Apply();

            return texture2D;
        }
    }
}