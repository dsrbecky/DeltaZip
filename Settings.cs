using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DeltaZip
{
    class Settings
    {
        public const int VersionMajor = 1;
        public const int VersionMinor = 0;

        public const string ArchiveExtension = ".dzp";
        public const string TmpExtension     = ".tmp";
        public const string TmpDirectory     = ".DeltaZip";
        public const string StateFile        = ".DeltaZipState";
        public const string MetaDataDir      = ".Metadata";
        public const string MetaDataInfo     = "info";
        public const string MetaDataFiles    = "files";
        public const string MetaDataHashes   = "hashes";
        public const string MetaDataStrings  = "strings";

        public static bool Create;
        public static bool CreateMulti;
        public static bool Extract;
        public static bool Verify;
        public static bool AutoQuit;

        public static string Src;
        public static string Dst;
        public static string Ref;
        public static bool RefRecent;

        public static string DefaultExtractSrc;
        public static string DefaultExtractDst;

        public static string MessageOfTheDay;

        public static string[] Exclude = { };

        public static int   CompressionLevel = 5;
        public static float CompressionTreshold = 0.80f;
        public static int   CompressionMinSize = 64 * 1024;
        public static bool  CompressHashes = false;
        public static int   CompressableTestSize = 512;
        public static int   CompressableTestSkip = 32 * 512;

        public static FileAttributes IgnoreAttributes = FileAttributes.Archive;
        
        public static int BlockSize = 16 * 1024;
        public static int SplitterReadBufferSize = 1 * 1024 * 1024;
        public static bool AsyncWrite = true;
        public static int MaxZipEntrySize = 2 * 1024 * 1024 + 4 * BlockSize;
        public static int MinSizeForParallelDeflate = 128 * 1024;
        public static int FileStreamBufferSize = 4 * 1024;
        public static int MaxQueuedWrites = 8;
        public static int WritePrefetchSize = 64 * 1024 * 1024;
        public static int WriteCacheSize = 512 * 1024 * 1024;
    }
}
