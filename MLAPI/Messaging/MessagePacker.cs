using System;

using UnityEngine;

using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using MLAPI.Security;
using BitStream = MLAPI.Serialization.BitStream;

using Isaac.Network;
using Isaac.Network.Messaging;

namespace MLAPI.Internal
{
	internal static class MessagePacker
	{
		private static readonly byte[] IV_BUFFER = new byte[16];
		private static readonly byte[] HMAC_BUFFER = new byte[32];
		private static readonly byte[] HMAC_PLACEHOLDER = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		// This method is responsible for unwrapping a message, that is extracting the messagebody.
		// Could include decrypting and/or authentication.
		internal static BitStream UnwrapMessage(BitStream inputStream, ulong clientID, out byte messageType, out SecuritySendFlags security)
		{
			using (PooledBitReader inputHeaderReader = PooledBitReader.Get(inputStream))
			{
				try
				{
					if (inputStream.Length < 1)
					{
						Debug.LogError("The incoming message was too small");
						messageType = (byte)MessageType.INVALID;
						security = SecuritySendFlags.None;
						return null;
					}

					bool isEncrypted = inputHeaderReader.ReadBit();
					bool isAuthenticated = inputHeaderReader.ReadBit();

					if (isEncrypted && isAuthenticated) security = SecuritySendFlags.Encrypted | SecuritySendFlags.Authenticated;
					else if (isEncrypted) security = SecuritySendFlags.Encrypted;
					else if (isAuthenticated) security = SecuritySendFlags.Authenticated;
					else security = SecuritySendFlags.None;
					
					messageType = inputHeaderReader.ReadByteBits(6);
					// The input stream is now ready to be read from. It's "safe" and has the correct position
					return inputStream;
				}
				catch (Exception e)
				{
					Debug.LogError("Error while unwrapping headers");
					Debug.LogError(e.ToString());

					security = SecuritySendFlags.None;
					messageType = (byte)MessageType.INVALID;
					return null;
				}
			}
		}

		internal static BitStream WrapMessage(byte messageType, ulong clientID, BitStream messageBody, SecuritySendFlags flags)
		{
			try
			{
				bool encrypted = ((flags & SecuritySendFlags.Encrypted) == SecuritySendFlags.Encrypted) && false; //NetworkManager.Get().NetworkConfig.EnableEncryption;
				bool authenticated = (flags & SecuritySendFlags.Authenticated) == SecuritySendFlags.Authenticated && false; //NetworkManager.Get().NetworkConfig.EnableEncryption;

				PooledBitStream outStream = PooledBitStream.Get();

				using (PooledBitWriter outWriter = PooledBitWriter.Get(outStream))
				{
					outWriter.WriteBit(encrypted);
					outWriter.WriteBit(authenticated);
					outWriter.WriteBits(messageType, 6);
					outStream.Write(messageBody.GetBuffer(), 0, (int)messageBody.Length);
				}

				return outStream;
			}
			catch (Exception e)
			{
				Debug.LogError("Error while wrapping headers");
				Debug.LogError(e.ToString());

				return null;
			}
		}
	}
}