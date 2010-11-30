using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DeltaZip
{
    class Settings
    {
        static Settings()
        {
            Conf.Deserializer.LoadAppConfig<Settings>();
        }

        public const int VersionMajor = 1;
        public const int VersionMinor = 0;

        public static int Compression = 5;
        public static float CompressionTreshold = 0.80f;
        public static string ArchiveExtension = ".dzp";
        public static string TmpExtension     = ".tmp";
        public static string MetaDataDir      = ".Metadata";
        public static string MetaDataInfo     = "info";
        public static string MetaDataFiles    = "files";
        public static string MetaDataHashes   = "hashes";
        public static string MetaDataStrings  = "strings";
        public static FileAttributes IgnoreAttributes = FileAttributes.Archive;
        public static bool CompressHashes = false;
        public static int SplitterBlockSize = 16 * 1024;
        public static int SplitterReadBufferSize = 1 * 1024 * 1024;
        public static bool AsyncWrite = true;
        public static int ZipEntrySize = 2 * 1024 * 1024;
        public static int MinSizeForParallelDeflate = 128 * 1024;
        public static int FileStreamBufferSize = 128 * 1024;
        public static int MaxQueuedWrites = 8;
        public static int WritePrefetchSize = 64 * 1024 * 1024;
        public static int WriteCacheSize = 512 * 1024 * 1024;
        public static int CompressableTestSize = 512;
        public static int CompressableTestSkip = 32 * 512;
        public static string[] Exclude = {};
    }
}
