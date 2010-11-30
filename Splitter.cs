using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace DeltaZip
{
    public struct Block
    {
        public readonly byte[] Buffer;
        public readonly int    Offset;
        public readonly int    Length;

        public Block(byte[] buffer, int offset, int length)
        {
            this.Buffer = buffer;
            this.Offset = offset;
            this.Length = length;
        }
    }

    /// <summary>
    /// CRC32 Based on the 0x04C11DB7 polynomial and little-endian order. (ie LSB of the bytes is send first)
    /// </summary>
    class Splitter
    {
        const uint RevPoly = 0xEDB88320;  // Reverse of 0x04C11DB7
        const int  HistoryLength  = 16;

        static uint[] crcTable = new uint[256];
        static uint[] crcTableHistory = new uint[256];

        static Splitter()
        {
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (uint j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ RevPoly;
                    else
                        crc = (crc >> 1);
                }
                crcTable[i] = crc;
            }

            for (uint i = 0; i < 256; i++) {
                uint crc = crcTable[i];
                for (int j = 0; j < HistoryLength; j++) {
                    Transform(ref crc, 0);
                }
                crcTableHistory[i] = crc;
            }
        }

        static void Transform(ref uint crc32, byte b)
        {
            crc32 = crcTable[b ^ (crc32 & 0xFF)] ^ (crc32 >> 8);
        }

        public static unsafe List<Block> Split(byte[] buffer, int offset, int length)
        {
            int blockSize = Settings.SplitterBlockSize;
            List<Block> blocks = new List<Block>(buffer.Length / blockSize * 3 / 2);
            int totalBlocksLength = 0;

            int MinBlockSize = blockSize / 4;
            int MaxBlockSize = blockSize * 16;

            uint hashMark = 0xFFFFFFFF - (0xFFFFFFFF / (uint)blockSize);
            int blockStart = offset;
            int pos = offset;

            while (pos < offset + length) {
                int start = Math.Min(pos + MinBlockSize, offset + length);
                int end   = Math.Min(pos + MaxBlockSize, offset + length);

                // Skip the MinBlockSize
                pos = start;

                // Calculate crc for the last HistoryLength bytes
                uint crcHistory = 0;
                for (int j = Math.Max(0, pos - HistoryLength); j < pos; j++) {   // buffer[i] was not processed yet
                    Transform(ref crcHistory, buffer[j]);
                }

                // Process the rest of block until we encounter mark
                while(pos < end && crcHistory < hashMark) {
                    crcHistory = crcTable[buffer[pos] ^ (crcHistory & 0xFF)] ^ (crcHistory >> 8) ^ // Add new byte
                                 crcTableHistory[buffer[pos - HistoryLength]];                     // Remove old byte

                    //uint crcCheck = 0;
                    //for (int j = pos - HistoryLength + 1; j <= pos; j++) {
                    //    Transform(ref crcCheck, buffer[j]);
                    //}
                    //if (crcCheck != crcHistory) throw new Exception("CrcHistory is wrong");

                    pos++;
                }

                // Report whatever we have processed
                int blockLength = pos - blockStart;
                blocks.Add(new Block(buffer, blockStart, blockLength));
                totalBlocksLength += blockLength;
                blockStart = pos;
            }

            if (totalBlocksLength != length)
                throw new Exception("Internal consistency error");

            return blocks;
        }

        public static IEnumerable<Block> Split(Stream stream, SHA1 sha1Provider)
        {
            byte[] buffer = new byte[Math.Min(Settings.SplitterReadBufferSize, stream.Length)];
            int    bufferUsed = 0;

            while(true) {
                // Fill up the buffer
                int readCount = stream.Read(buffer, bufferUsed, buffer.Length - bufferUsed);
                sha1Provider.TransformBlock(buffer, bufferUsed, readCount, buffer, bufferUsed);
                bufferUsed += readCount;

                List<Block> blocks = Split(buffer, 0, bufferUsed);

                if (stream.Position == stream.Length) {
                    // End of stream - flush and exit
                    foreach (Block hashBlock in blocks) {
                        yield return hashBlock;
                    }
                    break; 
                } else {
                    // Send all except the last block.  Keep the last block.
                    Block last = blocks[blocks.Count - 1];
                    blocks.RemoveAt(blocks.Count - 1);
                    foreach (Block hashBlock in blocks) {
                        yield return hashBlock;
                    }
					// Works fine with overlapping
                    Array.Copy(last.Buffer, last.Offset, buffer, 0, last.Length);
                    bufferUsed = last.Length;
                }
            }

            sha1Provider.TransformFinalBlock(new byte[0], 0, 0);
        }
    }
}