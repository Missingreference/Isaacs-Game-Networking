namespace MLAPI.Hashing
{
	/// <summary>
	/// Provides extension methods for getting hashes
	/// </summary>
	internal static class HashCode
	{
		private const uint FNV_offset_basis32 = 2166136261;
		private const uint FNV_prime32 = 16777619;

		private const ulong FNV_offset_basis64 = 14695981039346656037;
		private const ulong FNV_prime64 = 1099511628211;

        internal static ulong GetStableHash(this string str, HashMode hashMode)
        {
            switch(hashMode)
            {
                case HashMode.Hash16:
                    return GetStableHash16(str);
                case HashMode.Hash32:
                    return GetStableHash32(str);
                case HashMode.Hash64:
                    return GetStableHash64(str);
            }
            return 0;
        }

		/// <summary>
		/// non cryptographic stable hash code,  
		/// it will always return the same hash for the same
		/// string.  
		/// 
		/// This is simply an implementation of FNV-1 32 bit xor folded to 16 bit
		/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		/// </summary>
		/// <returns>The stable hash32.</returns>
		/// <param name="txt">Text.</param>
		internal static ushort GetStableHash16(this string txt)
		{
			uint hash32 = txt.GetStableHash32();

			return (ushort)((hash32 >> 16) ^ hash32);
		}


		/// <summary>
		/// non cryptographic stable hash code,  
		/// it will always return the same hash for the same
		/// string.  
		/// 
		/// This is simply an implementation of FNV-1 32 bit
		/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		/// </summary>
		/// <returns>The stable hash32.</returns>
		/// <param name="txt">Text.</param>
		internal static uint GetStableHash32(this string txt)
		{
			unchecked
			{
				uint hash = FNV_offset_basis32;
				for (int i = 0; i < txt.Length; i++)
				{
					uint ch = txt[i];
					hash = hash * FNV_prime32;
					hash = hash ^ ch;
				}
				return hash;
			}
		}

		/// <summary>
		/// non cryptographic stable hash code,  
		/// it will always return the same hash for the same
		/// string.  
		/// 
		/// This is simply an implementation of FNV-1  64 bit
		/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		/// </summary>
		/// <returns>The stable hash32.</returns>
		/// <param name="txt">Text.</param>
		internal static ulong GetStableHash64(this string txt)
		{
			unchecked
			{
				ulong hash = FNV_offset_basis64;
				for (int i = 0; i < txt.Length; i++)
				{
					ulong ch = txt[i];
					hash = hash * FNV_prime64;
					hash = hash ^ ch;
				}
				return hash;
			}
		}

		/// <summary>
		/// non cryptographic stable hash code,  
		/// it will always return the same hash for the same
		/// string.  
		/// 
		/// This is simply an implementation of FNV-1 32 bit xor folded to 16 bit
		/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		/// </summary>
		/// <returns>The stable hash32.</returns>
		/// <param name="bytes">Text.</param>
		internal static ushort GetStableHash16(this byte[] bytes)
		{
			uint hash32 = bytes.GetStableHash32();

			return (ushort)((hash32 >> 16) ^ hash32);
		}

		/// <summary>
		/// non cryptographic stable hash code,  
		/// it will always return the same hash for the same
		/// string.  
		/// 
		/// This is simply an implementation of FNV-1 32 bit
		/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		/// </summary>
		/// <returns>The stable hash32.</returns>
		/// <param name="bytes">Text.</param>
		internal static uint GetStableHash32(this byte[] bytes)
		{
			unchecked
			{
				uint hash = FNV_offset_basis32;
				for (int i = 0; i < bytes.Length; i++)
				{
					uint bt = bytes[i];
					hash = hash * FNV_prime32;
					hash = hash ^ bt;
				}
				return hash;
			}
		}

		/// <summary>
		/// non cryptographic stable hash code,  
		/// it will always return the same hash for the same
		/// string.  
		/// 
		/// This is simply an implementation of FNV-1  64 bit
		/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		/// </summary>
		/// <returns>The stable hash32.</returns>
		/// <param name="bytes">Text.</param>
		internal static ulong GetStableHash64(this byte[] bytes)
		{
			unchecked
			{
				ulong hash = FNV_offset_basis64;
				for (int i = 0; i < bytes.Length; i++)
				{
					ulong bt = bytes[i];
					hash = hash * FNV_prime64;
					hash = hash ^ bt;
				}
				return hash;
			}
		}
	}

    /// <summary>
	/// Represents the length of a var int encoded hash
	/// Note that the HashMode does not say anything about the actual final output due to the var int encoding
	/// It just says how many bytes the maximum will be
	/// </summary>
    public enum HashMode
    {
        Hash16, //2 bytes
        Hash32, //4 bytes
        Hash64  //8 bytes
    }
}