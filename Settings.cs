using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DeltaZip
{
    static class Settings
    {
        static Settings()
        {
        }

        public const int HashFileSignature = ((byte)'H' << 24) + ((byte)'A' << 16) + ((byte)'S' << 8) + ((byte)'H');
        public const int HashFileHashBlock = ((byte)'H' << 24) + ((byte)'A' << 16) + ((byte)'S' << 8) + ((byte)'H');
        public const int HashFileStringBlock = ((byte)'S' << 24) + ((byte)'T' << 16) + ((byte)'R' << 8) + ((byte)'S');

        public static int Compression = 5;
        public static float CompressionTreshold = 0.80f;
        public static string ArchiveExtension = ".dzp";
        public static string TmpExtension = ".tmp";
        public static string MetaDataDir = ".Metadata";
        public static string MetaDataFiles = "files";
        public static string MetaDataHashes = "hashes";
        public static FileAttributes IgnoreAttributes = FileAttributes.Archive;
        public static bool CompressHashes = false;
        public static bool AllowLocalReferences = true;
        public static int SplitterBlockSize = 16 * 1024;
        public static int SplitterReadBufferSize = 1 * 1024 * 1024;
        public static bool AsyncWrite = true;
        public static int MaxZipEntrySize = 2 * 1000 * 1000 + 16 * SplitterBlockSize;
        public static int MinSizeForParallelDeflate = 128 * 1024;
        public static int FileStreamBufferSize = 64 * 1024;
        public static int MaxQueuedWrites = 8;
        public static int ReadPrefetchCount = 32;
        public static int NumCacheLines = 256;
        public static int CompressableTestSize = 512;
        public static int CompressableTestSkip = 32 * 512;
        public static int DecompressionThreads = 8;
        public static string[] Exclude = { };
    }
}
