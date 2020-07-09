﻿#define ARRAY_WRITE_PERMISSIVE  // Allow attempt to write "packed" byte array (calls WriteByteArray())
#define ARRAY_RESOLVE_IMPLICIT  // Include WriteArray() method with automatic type resolution
#define ARRAY_WRITE_PREMAP      // Create a prefixed array diff mapping
#define ARRAY_DIFF_ALLOW_RESIZE // Whether or not to permit writing diffs of differently sized arrays

using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using MLAPI.Reflection;
#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
#endif

namespace MLAPI.Serialization
{
    /// <summary>
    /// A BinaryReader that can do bit wise manipulation when backed by a BitStream
    /// </summary>
    public class BitReader
    {
        private Stream source;
        private BitStream bitSource;

        /// <summary>
        /// Creates a new BitReader backed by a given stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        public BitReader(Stream stream)
        {
            source = stream;
            bitSource = stream as BitStream;
        }

        /// <summary>
        /// Changes the underlying stream the reader is reading from
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        public void SetStream(Stream stream)
        {
            source = stream;
            bitSource = stream as BitStream;
        }

        /// <summary>
        /// Reads a single byte
        /// </summary>
        /// <returns>The byte read as an integer</returns>
        public int ReadByte() => source.ReadByte();

        /// <summary>
        /// Reads a byte
        /// </summary>
        /// <returns>The byte read</returns>
        public byte ReadByteDirect() => (byte)source.ReadByte();

        /// <summary>
        /// Reads a single bit
        /// </summary>
        /// <returns>The bit read</returns>
        public bool ReadBit()
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            return bitSource.ReadBit();
        }

        /// <summary>
        /// Reads a single bit
        /// </summary>
        /// <returns>The bit read</returns>
        public bool ReadBool()
        {
            if (bitSource == null) return source.ReadByte() != 0;
            else return ReadBit();
        }

        /// <summary>
        /// Skips pad bits and aligns the position to the next byte
        /// </summary>
        public void SkipPadBits()
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            while (!bitSource.BitAligned) ReadBit();
        }

        [Obsolete]
        public object ReadObjectPacked(Type type)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read a single-precision floating point value from the stream.
        /// </summary>
        /// <returns>The read value</returns>
        public float ReadSingle()
        {
            return new UIntFloat
            {
                uintValue = ReadUInt32()
            }.floatValue;
        }


        /// <summary>
        /// Read a double-precision floating point value from the stream.
        /// </summary>
        /// <returns>The read value</returns>
        public double ReadDouble()
        {
            return new UIntFloat
            {
                ulongValue = ReadUInt64()
            }.doubleValue;
        }

        /// <summary>
        /// Read a single-precision floating point value from the stream from a varint
        /// </summary>
        /// <returns>The read value</returns>
        public float ReadSinglePacked()
        {
            return new UIntFloat
            {
                uintValue = ReadUInt32Packed()
            }.floatValue;
        }

        /// <summary>
        /// Read a double-precision floating point value from the stream as a varint
        /// </summary>
        /// <returns>The read value</returns>
        public double ReadDoublePacked()
        {
            return new UIntFloat
            {
                ulongValue = ReadUInt64Packed()
            }.doubleValue;
        }

        /// <summary>
        /// Read a single-precision floating point value from the stream. The value is between (inclusive) the minValue and maxValue.
        /// </summary>
        /// <param name="minValue">Minimum value that this value could be</param>
        /// <param name="maxValue">Maximum possible value that this could be</param>
        /// <param name="bytes">How many bytes the compressed value occupies. Must be between 1 and 4 (inclusive)</param>
        /// <returns>The read value</returns>
        public float ReadRangedSingle(float minValue, float maxValue, int bytes)
        {
            if (bytes < 1 || bytes > 4) throw new ArgumentOutOfRangeException("Result must occupy between 1 and 4 bytes!");
            uint read = 0;
            for (int i = 0; i < bytes; ++i) read |= (uint)ReadByte() << (i << 3);
            return (((float)read / ((0x100 * bytes) - 1)) * (minValue + maxValue)) - minValue;
        }

        /// <summary>
        /// read a double-precision floating point value from the stream. The value is between (inclusive) the minValue and maxValue.
        /// </summary>
        /// <param name="minValue">Minimum value that this value could be</param>
        /// <param name="maxValue">Maximum possible value that this could be</param>
        /// <param name="bytes">How many bytes the compressed value occupies. Must be between 1 and 8 (inclusive)</param>
        /// <returns>The read value</returns>
        public double ReadRangedDouble(double minValue, double maxValue, int bytes)
        {
            if (bytes < 1 || bytes > 8) throw new ArgumentOutOfRangeException("Result must occupy between 1 and 8 bytes!");
            ulong read = 0;
            for (int i = 0; i < bytes; ++i) read |= (ulong)ReadByte() << (i << 3);
            return (((double)read / ((0x100 * bytes) - 1)) * (minValue + maxValue)) - minValue;
        }

        /// <summary>
        /// Read a certain amount of bits from the stream.
        /// </summary>
        /// <param name="bitCount">How many bits to read. Minimum 0, maximum 8.</param>
        /// <returns>The bits that were read</returns>
        public ulong ReadBits(int bitCount)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (bitCount > 64) throw new ArgumentOutOfRangeException("Cannot read more than 64 bits into a 64-bit value!");
            if (bitCount < 0) throw new ArgumentOutOfRangeException("Cannot read fewer than 0 bits!");
            ulong read = 0;
            for (int i = 0; i + 8 < bitCount; i += 8) read |= (ulong)ReadByte() << i;
            read |= (ulong)ReadByteBits(bitCount & 7) << (bitCount & ~7);
            return read;
        }

        /// <summary>
        /// Read a certain amount of bits from the stream.
        /// </summary>
        /// <param name="bitCount">How many bits to read. Minimum 0, maximum 64.</param>
        /// <returns>The bits that were read</returns>
        public byte ReadByteBits(int bitCount)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (bitCount > 8) throw new ArgumentOutOfRangeException("Cannot read more than 8 bits into an 8-bit value!");
            if (bitCount < 0) throw new ArgumentOutOfRangeException("Cannot read fewer than 0 bits!");
            
            int result = 0;
            ByteBool convert = new ByteBool();
            for (int i = 0; i < bitCount; ++i)
                result |= convert.Collapse(ReadBit()) << i;
            return (byte) result;
        }

        /// <summary>
        /// Read a nibble (4 bits) from the stream.
        /// </summary>
        /// <param name="asUpper">Whether or not the nibble should be left-shifted by 4 bits</param>
        /// <returns>The nibble that was read</returns>
        public byte ReadNibble(bool asUpper)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            ByteBool convert = new ByteBool();
            
            byte result = (byte) (
                convert.Collapse(ReadBit()) |
                (convert.Collapse(ReadBit()) << 1) |
                (convert.Collapse(ReadBit()) << 2) |
                (convert.Collapse(ReadBit()) << 3)
            );
            if (asUpper) result <<= 4;
            return result;

        }

        // Marginally faster than the one that accepts a bool
        /// <summary>
        /// Read a nibble (4 bits) from the stream.
        /// </summary>
        /// <returns>The nibble that was read</returns>
        public byte ReadNibble()
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            ByteBool convert = new ByteBool();
            return (byte) (
                convert.Collapse(ReadBit()) |
                (convert.Collapse(ReadBit()) << 1) |
                (convert.Collapse(ReadBit()) << 2) |
                (convert.Collapse(ReadBit()) << 3)
            );
        }

        /// <summary>
        /// Reads a signed byte
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public sbyte ReadSByte() => (sbyte)ReadByte();
        /// <summary>
        /// Read an unsigned short (UInt16) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public ushort ReadUInt16() => (ushort)(ReadByte() | (ReadByte() << 8));
        /// <summary>
        /// Read a signed short (Int16) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public short ReadInt16() => (short)ReadUInt16();
        /// <summary>
        /// Read a single character from the stream
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public char ReadChar() => (char)ReadUInt16();
        /// <summary>
        /// Read an unsigned int (UInt32) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public uint ReadUInt32() => (uint)(ReadByte() | (ReadByte() << 8) | (ReadByte() << 16) | (ReadByte() << 24));
        /// <summary>
        /// Read a signed int (Int32) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public int ReadInt32() => (int)ReadUInt32();
        /// <summary>
        /// Read an unsigned long (UInt64) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public ulong ReadUInt64() => (
                ((uint)ReadByte()) |
                ((ulong)ReadByte() << 8) |
                ((ulong)ReadByte() << 16) |
                ((ulong)ReadByte() << 24) |
                ((ulong)ReadByte() << 32) |
                ((ulong)ReadByte() << 40) |
                ((ulong)ReadByte() << 48) |
                ((ulong)ReadByte() << 56)
                );
        /// <summary>
        /// Read a signed long (Int64) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public long ReadInt64() => (long)ReadUInt64();

        /// <summary>
        /// Read a ZigZag encoded varint signed short (Int16) from the stream.
        /// </summary>
        /// <returns>Decoded un-varinted value.</returns>
        public short ReadInt16Packed() => (short)Arithmetic.ZigZagDecode(ReadUInt64Packed());
        /// <summary>
        /// Read a varint unsigned short (UInt16) from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public ushort ReadUInt16Packed() => (ushort)ReadUInt64Packed();
        /// <summary>
        /// Read a varint two-byte character from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public char ReadCharPacked() => (char)ReadUInt16Packed();
        /// <summary>
        /// Read a ZigZag encoded varint signed int (Int32) from the stream.
        /// </summary>
        /// <returns>Decoded un-varinted value.</returns>
        public int ReadInt32Packed() => (int)Arithmetic.ZigZagDecode(ReadUInt64Packed());
        /// <summary>
        /// Read a varint unsigned int (UInt32) from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public uint ReadUInt32Packed() => (uint)ReadUInt64Packed();
        /// <summary>
        /// Read a ZigZag encoded varint signed long(Int64) from the stream.
        /// </summary>
        /// <returns>Decoded un-varinted value.</returns>
        public long ReadInt64Packed() => Arithmetic.ZigZagDecode(ReadUInt64Packed());
        /// <summary>
        /// Read a varint unsigned long (UInt64) from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public ulong ReadUInt64Packed()
        {
            ulong header = ReadByteDirect();
            if (header <= 240) return header;
            if (header <= 248) return 240 + ((header - 241) << 8) + ReadByteDirect();
            if (header == 249) return 2288UL + (ulong)(ReadByte() << 8) + ReadByteDirect();
            ulong res = ReadByteDirect() | ((ulong)ReadByteDirect() << 8) | ((ulong)ReadByte() << 16);
            int cmp = 2;
            int hdr = (int)(header - 247);
            while (hdr > ++cmp) res |= (ulong)ReadByte() << (cmp << 3);
            return res;
        }

        // Read arrays

        /// <summary>
        /// Read a string from the stream.
        /// </summary>
        /// <returns>The string that was read.</returns>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadString(bool oneByteChars) => ReadString(null, oneByteChars);
        /// <summary>
        /// Read a string from the stream.
        /// </summary>
        /// <returns>The string that was read.</returns>
        /// <param name="builder">The builder to read the values into or null to use a new builder.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadString(StringBuilder builder = null, bool oneByteChars = false)
        {
            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null) builder = new StringBuilder(expectedLength);
            else if (builder.Capacity + builder.Length < expectedLength) builder.Capacity = expectedLength + builder.Length;
            for (int i = 0; i < expectedLength; ++i)
                builder.Insert(i, oneByteChars ? (char)ReadByte() : ReadChar());
            return builder;
        }
        /// <summary>
        /// Read string encoded as a varint from the stream.
        /// </summary>
        /// <returns>The string that was read.</returns>
        /// <param name="builder">The builder to read the string into or null to use a new builder</param>
        public StringBuilder ReadStringPacked(StringBuilder builder = null)
        {
            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null) builder = new StringBuilder(expectedLength);
            else if (builder.Capacity + builder.Length < expectedLength) builder.Capacity = expectedLength + builder.Length;
            for (int i = 0; i < expectedLength; ++i)
                builder.Insert(i, ReadCharPacked());
            return builder;
        }
        /// <summary>
        /// Read string diff from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compare">The version to compare the diff to.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadStringDiff(string compare, bool oneByteChars = false) => ReadStringDiff(null, compare, oneByteChars);
        /// <summary>
        /// Read string diff from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version</returns>
        /// <param name="builder">The builder to read the string into or null to use a new builder.</param>
        /// <param name="compare">The version to compare the diff to.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadStringDiff(StringBuilder builder, string compare, bool oneByteChars = false)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null) builder = new StringBuilder(expectedLength);
            else if (builder.Capacity < expectedLength) builder.Capacity = expectedLength;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(compare == null ? 0 : Math.Min(expectedLength, compare.Length));
            ulong mapStart;
            int compareLength = compare == null ? 0 : compare.Length;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    builder.Insert(i, oneByteChars ? (char)ReadByte() : ReadChar());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < compareLength) builder.Insert(i, compare[i]);
            }
            bitSource.BitPosition = dBlockStart;
            return builder;
        }
        /// <summary>
        /// Read string diff from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compareAndBuffer">The builder containing the current version and that will also be used as the output buffer.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars will be used and only ASCII will be supported.</param>
        public StringBuilder ReadStringDiff(StringBuilder compareAndBuffer, bool oneByteChars = false)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            int expectedLength = (int)ReadUInt32Packed();
            if (compareAndBuffer == null) throw new ArgumentNullException("Buffer cannot be null");
            else if (compareAndBuffer.Capacity < expectedLength) compareAndBuffer.Capacity = expectedLength;
            ulong dBlockStart = bitSource.BitPosition + (ulong)Math.Min(expectedLength, compareAndBuffer.Length);
            ulong mapStart;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareAndBuffer.Length || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    compareAndBuffer.Remove(i, 1);
                    compareAndBuffer.Insert(i, oneByteChars ? (char)ReadByte() : ReadChar());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
            }
            bitSource.BitPosition = dBlockStart;
            return compareAndBuffer;
        }
        /// <summary>
        /// Read string diff encoded as varints from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compare">The version to compare the diff to.</param>
        public StringBuilder ReadStringPackedDiff(string compare) => ReadStringPackedDiff(null, compare);
        /// <summary>
        /// Read string diff encoded as varints from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version</returns>
        /// <param name="builder">The builder to read the string into or null to use a new builder.</param>
        /// <param name="compare">The version to compare the diff to.</param>
        public StringBuilder ReadStringPackedDiff(StringBuilder builder, string compare)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null) builder = new StringBuilder(expectedLength);
            else if (builder.Capacity < expectedLength) builder.Capacity = expectedLength;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(compare == null ? 0 : Math.Min(expectedLength, compare.Length));
            ulong mapStart;
            int compareLength = compare == null ? 0 : compare.Length;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    builder.Insert(i, ReadCharPacked());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < compareLength) builder.Insert(i, compare[i]);
            }
            bitSource.BitPosition = dBlockStart;
            return builder;
        }
        /// <summary>
        /// Read string diff encoded as varints from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compareAndBuffer">The builder containing the current version and that will also be used as the output buffer.</param>
        public StringBuilder ReadStringPackedDiff(StringBuilder compareAndBuffer)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            int expectedLength = (int)ReadUInt32Packed();
            if (compareAndBuffer == null) throw new ArgumentNullException("Buffer cannot be null");
            else if (compareAndBuffer.Capacity < expectedLength) compareAndBuffer.Capacity = expectedLength;
            ulong dBlockStart = bitSource.BitPosition + (ulong)Math.Min(expectedLength, compareAndBuffer.Length);
            ulong mapStart;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareAndBuffer.Length || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    compareAndBuffer.Remove(i, 1);
                    compareAndBuffer.Insert(i, ReadCharPacked());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
            }
            bitSource.BitPosition = dBlockStart;
            return compareAndBuffer;
        }
        /// <summary>
        /// Read byte array into an optional buffer from the stream.
        /// </summary>
        /// <returns>The byte array that has been read.</returns>
        /// <param name="readTo">The array to read into. If the array is not large enough or if it's null. A new array is created.</param>
        /// <param name="knownLength">The length of the array if it's known. Otherwise -1</param>
        public byte[] ReadByteArray(byte[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new byte[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadByteDirect();
            return readTo;
        }
        /// <summary>
        /// Read byte array diff into an optional buffer from the stream.
        /// </summary>
        /// <returns>The byte array created from the diff and original.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The length of the array if it's known. Otherwise -1</param>
        public byte[] ReadByteArrayDiff(byte[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            byte[] writeTo = readTo == null || readTo.LongLength != knownLength ? new byte[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadByteDirect();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read short array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArray(short[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new short[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadInt16();
            return readTo;
        }
        /// <summary>
        /// Read short array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArrayPacked(short[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new short[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadInt16Packed();
            return readTo;
        }
        /// <summary>
        /// Read short array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArrayDiff(short[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            short[] writeTo = readTo == null || readTo.LongLength != knownLength ? new short[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadInt16();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read short array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArrayPackedDiff(short[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            short[] writeTo = readTo == null || readTo.LongLength != knownLength ? new short[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadInt16Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
        /// <summary>
        /// Read ushort array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArray(ushort[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new ushort[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadUInt16();
            return readTo;
        }
        /// <summary>
        /// Read ushort array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArrayPacked(ushort[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new ushort[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadUInt16Packed();
            return readTo;
        }
        /// <summary>
        /// Read ushort array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArrayDiff(ushort[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            ushort[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ushort[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt16();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read ushort array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArrayPackedDiff(ushort[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            ushort[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ushort[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt16Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
        /// <summary>
        /// Read int array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArray(int[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new int[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadInt32();
            return readTo;
        }
        /// <summary>
        /// Read int array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArrayPacked(int[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new int[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadInt32Packed();
            return readTo;
        }
        /// <summary>
        /// Read int array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArrayDiff(int[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            int[] writeTo = readTo == null || readTo.LongLength != knownLength ? new int[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadInt32();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read int array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArrayPackedDiff(int[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            int[] writeTo = readTo == null || readTo.LongLength != knownLength ? new int[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadInt32Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
        /// <summary>
        /// Read uint array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public uint[] ReadUIntArray(uint[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new uint[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadUInt32();
            return readTo;
        }
        /// <summary>
        /// Read uint array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public uint[] ReadUIntArrayPacked(uint[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new uint[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadUInt32Packed();
            return readTo;
        }
        /// <summary>
        /// Read uint array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public uint[] ReadUIntArrayDiff(uint[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            uint[] writeTo = readTo == null || readTo.LongLength != knownLength ? new uint[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt32();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read long array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArray(long[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new long[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadInt64();
            return readTo;
        }
        /// <summary>
        /// Read long array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArrayPacked(long[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new long[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadInt64Packed();
            return readTo;
        }
        /// <summary>
        /// Read long array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArrayDiff(long[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            long[] writeTo = readTo == null || readTo.LongLength != knownLength ? new long[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadInt64();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read long array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArrayPackedDiff(long[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            long[] writeTo = readTo == null || readTo.LongLength != knownLength ? new long[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadInt64Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
        /// <summary>
        /// Read ulong array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArray(ulong[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new ulong[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadUInt64();
            return readTo;
        }
        /// <summary>
        /// Read ulong array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArrayPacked(ulong[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new ulong[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadUInt64Packed();
            return readTo;
        }
        /// <summary>
        /// Read ulong array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArrayDiff(ulong[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            ulong[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ulong[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt64();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read ulong array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArrayPackedDiff(ulong[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            ulong[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ulong[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt64Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
        /// <summary>
        /// Read float array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArray(float[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new float[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadSingle();
            return readTo;
        }
        /// <summary>
        /// Read float array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArrayPacked(float[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new float[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadSinglePacked();
            return readTo;
        }
        /// <summary>
        /// Read float array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArrayDiff(float[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            float[] writeTo = readTo == null || readTo.LongLength != knownLength ? new float[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadSingle();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read float array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArrayPackedDiff(float[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            float[] writeTo = readTo == null || readTo.LongLength != knownLength ? new float[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    readTo[i] = ReadSinglePacked();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
        /// <summary>
        /// Read double array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArray(double[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new double[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadDouble();
            return readTo;
        }
        /// <summary>
        /// Read double array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArrayPacked(double[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            if (readTo == null || readTo.LongLength != knownLength) readTo = new double[knownLength];
            for (long i = 0; i < knownLength; ++i) readTo[i] = ReadDoublePacked();
            return readTo;
        }
        /// <summary>
        /// Read double array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArrayDiff(double[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            double[] writeTo = readTo == null || readTo.LongLength != knownLength ? new double[knownLength] : readTo;
            ulong dBlockStart = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = bitSource.BitPosition;
                    bitSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadDouble();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = bitSource.BitPosition;
                    // Return to mapping section
                    bitSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = dBlockStart;
            return writeTo;
        }
        /// <summary>
        /// Read double array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArrayPackedDiff(double[] readTo = null, long knownLength = -1)
        {
            if (bitSource == null) throw new InvalidOperationException("Cannot read bits on a non BitStream stream");
            if (knownLength < 0) knownLength = (long)ReadUInt64Packed();
            double[] writeTo = readTo == null || readTo.LongLength != knownLength ? new double[knownLength] : readTo;
            ulong data = bitSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo == null ? 0 : readTo.LongLength;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = bitSource.BitPosition;
                    bitSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadDoublePacked();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = bitSource.BitPosition;
                    bitSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength) writeTo[i] = readTo[i];
            }
            bitSource.BitPosition = data;
            return writeTo;
        }
    }
}
