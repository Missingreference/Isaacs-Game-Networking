using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using MLAPI.Serialization;

namespace Elanetic.Network.Serialization
{
    public static class SerializeUnityTypes
    {
        #region Texture2D

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

        #endregion Texture2D

        #region Vector2

        /// <summary>
        /// Convenience method that writes two non-varint floats from the vector to the stream
        /// </summary>
        /// <param name="vector2">Vector to write</param>
        static public void WriteVector2(this BitWriter writer, Vector2 vector2)
        {
            writer.WriteSingle(vector2.x);
            writer.WriteSingle(vector2.y);
        }

        /// <summary>
        /// Convenience method that writes two varint floats from the vector to the stream
        /// </summary>
        /// <param name="vector2">Vector to write</param>
        static public void WriteVector2Packed(this BitWriter writer, Vector2 vector2)
        {
            writer.WriteSinglePacked(vector2.x);
            writer.WriteSinglePacked(vector2.y);
        }

        /// <summary>
        /// Read a Vector2 from the stream.
        /// </summary>
        /// <returns>The Vector2 read from the stream.</returns>
        static public Vector2 ReadVector2(this BitReader reader) => new Vector2(reader.ReadSingle(), reader.ReadSingle());

        /// <summary>
        /// Read a Vector2 from the stream.
        /// </summary>
        /// <returns>The Vector2 read from the stream.</returns>
        static public Vector2 ReadVector2Packed(this BitReader reader) => new Vector2(reader.ReadSinglePacked(), reader.ReadSinglePacked());

        #endregion Vector2

        #region Vector3

        /// <summary>
        /// Convenience method that writes three non-varint floats from the vector to the stream
        /// </summary>
        /// <param name="vector3">Vector to write</param>
        static public void WriteVector3(this BitWriter writer, Vector3 vector3)
        {
            writer.WriteSingle(vector3.x);
            writer.WriteSingle(vector3.y);
            writer.WriteSingle(vector3.z);
        }

        /// <summary>
        /// Convenience method that writes three varint floats from the vector to the stream
        /// </summary>
        /// <param name="vector3">Vector to write</param>
        static public void WriteVector3Packed(this BitWriter writer, Vector3 vector3)
        {
            writer.WriteSinglePacked(vector3.x);
            writer.WriteSinglePacked(vector3.y);
            writer.WriteSinglePacked(vector3.z);
        }

        /// <summary>
        /// Read a Vector3 from the stream.
        /// </summary>
        /// <returns>The Vector3 read from the stream.</returns>
        static public Vector3 ReadVector3(this BitReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        /// <summary>
        /// Read a Vector3 from the stream.
        /// </summary>
        /// <returns>The Vector3 read from the stream.</returns>
        static public Vector3 ReadVector3Packed(this BitReader reader) => new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());

        #endregion Vector3

        #region Vector4

        /// <summary>
        /// Convenience method that writes four non-varint floats from the vector to the stream
        /// </summary>
        /// <param name="vector4">Vector to write</param>
        static public void WriteVector4(this BitWriter writer, Vector4 vector4)
        {
            writer.WriteSingle(vector4.x);
            writer.WriteSingle(vector4.y);
            writer.WriteSingle(vector4.z);
            writer.WriteSingle(vector4.w);
        }

        /// <summary>
        /// Convenience method that writes four varint floats from the vector to the stream
        /// </summary>
        /// <param name="vector4">Vector to write</param>
        static public void WriteVector4Packed(this BitWriter writer, Vector4 vector4)
        {
            writer.WriteSinglePacked(vector4.x);
            writer.WriteSinglePacked(vector4.y);
            writer.WriteSinglePacked(vector4.z);
            writer.WriteSinglePacked(vector4.w);
        }

        /// <summary>
        /// Read a Vector4 from the stream.
        /// </summary>
        /// <returns>The Vector4 read from the stream.</returns>
        static public Vector4 ReadVector4(this BitReader reader) => new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        /// <summary>
        /// Read a Vector4 from the stream.
        /// </summary>
        /// <returns>The Vector4 read from the stream.</returns>
        static public Vector4 ReadVector4Packed(this BitReader reader) => new Vector4(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());

        #endregion Vector4

        #region Color

        /// <summary>
        /// Convenience method that writes four non-varint floats from the color to the stream
        /// </summary>
        /// <param name="color">Color to write</param>
        static public void WriteColor(this BitWriter writer, Color color)
        {
            writer.WriteSingle(color.r);
            writer.WriteSingle(color.g);
            writer.WriteSingle(color.b);
            writer.WriteSingle(color.a);
        }

        /// <summary>
        /// Convenience method that writes four varint floats from the color to the stream
        /// </summary>
        /// <param name="color">Color to write</param>
        static public void WriteColorPacked(this BitWriter writer, Color color)
        {
            writer.WriteSinglePacked(color.r);
            writer.WriteSinglePacked(color.g);
            writer.WriteSinglePacked(color.b);
            writer.WriteSinglePacked(color.a);
        }

        /// <summary>
        /// Convenience method that writes four non-varint floats from the color to the stream
        /// </summary>
        /// <param name="color32">Color32 to write</param>
        static public void WriteColor32(this BitWriter writer, Color32 color32)
        {
            writer.WriteSingle(color32.r);
            writer.WriteSingle(color32.g);
            writer.WriteSingle(color32.b);
            writer.WriteSingle(color32.a);
        }

        /// <summary>
        /// Read a Color from the stream.
        /// </summary>
        /// <returns>The Color read from the stream.</returns>
        static public Color ReadColor(this BitReader reader) => new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        /// <summary>
        /// Read a Color from the stream.
        /// </summary>
        /// <returns>The Color read from the stream.</returns>
        static public Color ReadColorPacked(this BitReader reader) => new Color(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());

        /// <summary>
        /// Read a Color32 from the stream.
        /// </summary>
        /// <returns>The Color32 read from the stream.</returns>
        static public Color32 ReadColor32(this BitReader reader) => new Color32((byte)reader.ReadByte(), (byte)reader.ReadByte(), (byte)reader.ReadByte(), (byte)reader.ReadByte());

        #endregion Color

        #region Ray

        /// <summary>
        /// Convenience method that writes two non-packed Vector3 from the ray to the stream
        /// </summary>
        /// <param name="ray">Ray to write</param>
        static public void WriteRay(this BitWriter writer, Ray ray)
        {
            writer.WriteVector3(ray.origin);
            writer.WriteVector3(ray.direction);
        }

        /// <summary>
        /// Convenience method that writes two packed Vector3 from the ray to the stream
        /// </summary>
        /// <param name="ray">Ray to write</param>
        static public void WriteRayPacked(this BitWriter writer, Ray ray)
        {
            writer.WriteVector3Packed(ray.origin);
            writer.WriteVector3Packed(ray.direction);
        }

        /// <summary>
        /// Read a Ray from the stream.
        /// </summary>
        /// <returns>The Ray read from the stream.</returns>
        static public Ray ReadRay(this BitReader reader) => new Ray(reader.ReadVector3(), reader.ReadVector3());

        /// <summary>
        /// Read a Ray from the stream.
        /// </summary>
        /// <returns>The Ray read from the stream.</returns>
        static public Ray ReadRayPacked(this BitReader reader) => new Ray(reader.ReadVector3Packed(), reader.ReadVector3Packed());

        #endregion Ray

        #region Quaternion

        /// <summary>
        /// Writes the rotation to the stream.
        /// </summary>
        /// <param name="rotation">Rotation to write</param>
        static public void WriteRotationPacked(this BitWriter writer, Quaternion rotation)
        {
            if(Mathf.Sign(rotation.w) < 0)
            {
                writer.WriteSinglePacked(-rotation.x);
                writer.WriteSinglePacked(-rotation.y);
                writer.WriteSinglePacked(-rotation.z);
            }
            else
            {
                writer.WriteSinglePacked(rotation.x);
                writer.WriteSinglePacked(rotation.y);
                writer.WriteSinglePacked(rotation.z);
            }
        }

        /// <summary>
        /// Writes the rotation to the stream.
        /// </summary>
        /// <param name="rotation">Rotation to write</param>
        static public void WriteRotation(this BitWriter writer, Quaternion rotation)
        {
            writer.WriteSingle(rotation.x);
            writer.WriteSingle(rotation.y);
            writer.WriteSingle(rotation.z);
            writer.WriteSingle(rotation.w);
        }

        /// <summary>
        /// Reads the rotation from the stream
        /// </summary>
        /// <returns>The rotation read from the stream</returns>
        static public Quaternion ReadRotationPacked(this BitReader reader)
        {
            float x = reader.ReadSinglePacked();
            float y = reader.ReadSinglePacked();
            float z = reader.ReadSinglePacked();

            float w = Mathf.Sqrt(1 - ((Mathf.Pow(x, 2) - (Mathf.Pow(y, 2) - (Mathf.Pow(z, 2))))));

            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Reads the rotation from the stream
        /// </summary>
        /// <returns>The rotation read from the stream</returns>
        static public Quaternion ReadRotation(this BitReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float w = reader.ReadSingle();

            return new Quaternion(x, y, z, w);
        }

        #endregion Quaternion
    }
}