using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Security.Cryptography;
using Ionic.Zip;
using Ionic.Zlib;
using System.Xml.Serialization;

namespace DeltaZip
{
    public class Reference
    {
        string archiveName;
        HashSource[] hashesSorted;
        MemoryStream strings;

        public Reference(string archiveName, HashSource[] hashesSorted, MemoryStream strings)
        {
            this.archiveName = archiveName;
            this.hashesSorted = hashesSorted;
            this.strings = strings;
        }

        public Reference(string filename)
        {
            archiveName = Path.GetFileName(filename);

            ZipFile zipFile = new ZipFile(filename);
            hashesSorted = Util.ReadArray<HashSource>(Util.ExtractMetaData(zipFile, Settings.MetaDataHashes));
            strings = Util.ReadStream(Util.ExtractMetaData(zipFile, Settings.MetaDataStrings));
            Array.Sort(hashesSorted);
        }

        public bool TryFindHash(Hash hash, out HashSource hashSource, out string path)
        {
            int hashIndex = Array.BinarySearch(hashesSorted, new HashSource() { Hash = hash });
            if (hashIndex >= 0) {
                hashSource = hashesSorted[hashIndex];

                // Get the path
                path = GetString(hashSource.Path);
                hashSource.Path = -1;
                if (path.StartsWith(@"\")) path = archiveName + path;

                return true;
            } else {
                // Not found
                hashSource = new HashSource();
                path = string.Empty;
                return false;
            }
        }

        public string GetString(int index)
        {
            strings.Position = index;
            List<byte> nameBytes = new List<byte>();
            int b;
            while ((b = strings.ReadByte()) > 0) nameBytes.Add((byte)b);
            return Encoding.UTF8.GetString(nameBytes.ToArray());
        }
    }

    public class ArchiveWriter
    {
        Stats stats;
        FileStream baseStream;
        ZipOutputStream zipStream;

        List<File> files = new List<File>();

        List<HashSource>        hashes = new List<HashSource>();
        Dictionary<Hash, int>   hashesLookup = new Dictionary<Hash, int>();

        MemoryStream            strings = new MemoryStream();
        Dictionary<string, int> stringsLookup = new Dictionary<string, int>();

        public class Stats
        {
            public string Title;
            public string Status;
            public float Progress;
            public DateTime StartTime = DateTime.Now;
            public DateTime? EndTime;

            public long Compressed;
            public long SavedByCompression;
            public long SavedByInternalDelta;
            public long SavedByExternalDelta;

            public volatile bool Canceled;
        }

        public ArchiveWriter(string filename, Stats stats)
        {
            this.stats = stats;
            this.baseStream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None, Settings.FileStreamBufferSize);
            this.zipStream = new ZipOutputStream(this.baseStream);
            this.zipStream.EnableZip64 = Zip64Option.AsNecessary;
            this.zipStream.ParallelDeflateThreshold = 0; // Force enabled
            this.zipStream.UseUnicodeAsNecessary = true;   
        }

        public void AddDir(string path, Reference reference)
        {
            this.stats.EndTime = null;
            stats.Status = "Reading directory " + path + "...";
            path = Path.GetFullPath(path);
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            // Strip path to reduce memory
            for (int i = 0; i < files.Length; i++) {
                if (files[i].StartsWith(path)) {
                    files[i] = files[i].Substring(path.Length).TrimStart('\\');
                }
            }

            List<string> exclude = new List<string>();
            foreach(string ex in Settings.Exclude) {
                exclude.Add(ex.ToLowerInvariant());
            }

            foreach (string file in files) {
                if (stats.Canceled) return;
                if (exclude.Contains(Path.GetFileName(file).ToLowerInvariant())) continue;

                AddFile(path, file, reference);
            }
        }

        public void AddFile(string root, string filename, Reference reference)
        {
            string fullFilename = Path.Combine(root, filename);
            using (FileStream fileStreamIn = new FileStream(fullFilename, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileStreamBufferSize, true)) {
                stats.Status = "Compressing " + filename;
                stats.Progress = 0;

                BufferName = filename;
                BufferNameSuffix = 0;
                BufferNameForceAppendSuffix = false;

                SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                // Store
                List<int> hashIndices = new List<int>();
                long offset = 0;
                foreach (Block block in Splitter.Split(fileStreamIn, sha1Provider, false)) {
                    if (stats.Canceled) return;

                    Hash hash = Hash.Compute(block);

                    HashSource referenceHash;
                    string     referenceHashPath;

                    int hashIndex;
                    if (hashesLookup.TryGetValue(hash, out hashIndex)) {
                        // Internal reference
                        stats.SavedByInternalDelta += block.Length;
                        BufferNameForceAppendSuffix = true;
                    } else if (reference != null && reference.TryFindHash(hash, out referenceHash, out referenceHashPath)) {
                        // Reference to existing archive
                        stats.SavedByExternalDelta += block.Length;
                        BufferNameForceAppendSuffix = true;
                        // Copy the hash to local array
                        referenceHash.Path = this.AddString(referenceHashPath);
                        hashes.Add(referenceHash);
                        hashIndex = hashes.Count - 1;
                        hashesLookup[hash] = hashIndex;
                    } else {
                        // Store the data locally
                        hashIndex = WriteToBuffer(hash, block);
                        hashesLookup[hash] = hashIndex;
                    }
                    hashIndices.Add(hashIndex);

                    offset += block.Length;

                    stats.Progress = (float)offset / (float)fileStreamIn.Length;
                };
                if (offset != fileStreamIn.Length)
                    throw new Exception("Internal consistency error");

                FlushBuffer(true);

                FileInfo fileInfo = new FileInfo(fullFilename);

                File file = new File() {
                    Name = filename,
                    Size = fileInfo.Length,
                    Hash = sha1Provider.Hash,
                    Created = fileInfo.CreationTime,
                    Modified = fileInfo.LastWriteTime,
                    Attributes = fileInfo.Attributes & ~Settings.IgnoreAttributes,
                    HashIndices = hashIndices
                };
                files.Add(file);
            }
        }

        int AddString(string text)
        {
            int index;
            if (!stringsLookup.TryGetValue(text, out index)) {
                index = (int)strings.Position;
                stringsLookup[text] = index;
                byte[] stringData = Encoding.UTF8.GetBytes(text);
                strings.Write(stringData, 0, stringData.Length);
                strings.WriteByte(0);
            }
            return index;
        }

        WorkerThread workerThread = new WorkerThread();

        public string BufferName;
        public int    BufferNameSuffix = 0;
        public bool   BufferNameForceAppendSuffix;

        MemoryStream WriteBuffer = StreamPool.Allocate();
        List<int>    HashIndexesForPatching = new List<int>();

        int WriteToBuffer(Hash hash, Block block)
        {
            if (WriteBuffer.Length + block.Length > Settings.MaxZipEntrySize) {
                FlushBuffer(false);
            }

            HashSource hashSource = new HashSource() {
                Hash = hash,
                Path = -1,
                Offset = (int)WriteBuffer.Length,
                Length = block.Length,
            };

            WriteBuffer.Write(block.Buffer, block.Offset, block.Length);

            hashes.Add(hashSource);
            int hashIndex = hashes.Count - 1;
            HashIndexesForPatching.Add(hashIndex);
            return hashIndex;
        }

        void FlushBuffer(bool final)
        {
            if (WriteBuffer.Length > 0) {
                // Name the entry
                string nameWithSuffix = (final && BufferNameSuffix == 0 && !BufferNameForceAppendSuffix) ? BufferName : (BufferName + "~" + BufferNameSuffix.ToString("D3"));
                BufferNameSuffix++;

                // Patch hashes
                foreach (int hashIndex in HashIndexesForPatching) {
                    HashSource copy = hashes[hashIndex];
                    copy.Path = AddString(@"\" + nameWithSuffix);
                    hashes[hashIndex] = copy;
                }
                HashIndexesForPatching.Clear();

                // Content of entity
                MemoryStream entityContent = this.WriteBuffer;

                // Allocate new tmp stream
                this.WriteBuffer = StreamPool.Allocate();

                MethodInvoker writeMethod = delegate {
                    bool compress = entityContent.Length > Settings.CompressionMinSize && Util.IsCompressable(entityContent);
                    long oldCompressedSize = baseStream.Position;

                    zipStream.ParallelDeflateThreshold = entityContent.Length >= Settings.MinSizeForParallelDeflate ? 0 /* on */ : -1 /* off */;
                    ZipEntry entry = zipStream.PutNextEntry(nameWithSuffix);
                    entry.CompressionLevel = compress ? (CompressionLevel)Settings.CompressionLevel : CompressionLevel.None;
                    entityContent.Position = 0;
                    entityContent.WriteTo(zipStream);
                    zipStream.Flush();

                    long compressedSize = baseStream.Position - oldCompressedSize;
                    stats.Compressed += compressedSize;
                    stats.SavedByCompression += entityContent.Length - compressedSize;

                    StreamPool.Release(ref entityContent);
                };

                workerThread.Enqueue(writeMethod);
            }
        }

        public unsafe Reference Finish(string filename)
        {
            // Release memory
            workerThread.WaitUntilDoneAndExit();
            StreamPool.Release(ref WriteBuffer);
            hashesLookup = new Dictionary<Hash, int>();
            stringsLookup = new Dictionary<string, int>();

            stats.Status = "Writing metadata";
            stats.Progress = 0;

            // Write info
            {
                Info info = new Info() {
                    Version = new Version { Major = Settings.VersionMajor, Minor = Settings.VersionMinor },
                    CreatedBy = "DeltaZip",
                    CreatedOn = DateTime.Now,
                    Comment = null
                };
                ZipEntry entry = zipStream.PutNextEntry(Path.Combine(Settings.MetaDataDir, Settings.MetaDataInfo));
                entry.CompressionLevel = (CompressionLevel)Settings.CompressionLevel;
                new XmlSerializer(typeof(Info)).Serialize(new StreamWriter(zipStream, Encoding.UTF8), info);
            }

            // Write file list
            {
                ZipEntry entry = zipStream.PutNextEntry(Path.Combine(Settings.MetaDataDir, Settings.MetaDataFiles));
                entry.CompressionLevel = (CompressionLevel)Settings.CompressionLevel;
                Util.FileSerializer.Serialize(new StreamWriter(zipStream, Encoding.UTF8), files);

                // Release memory
                files = new List<File>();
            }

            // Write hashes
            HashSource[] hashesCopy;
            {
                // Sort
                hashesCopy = hashes.ToArray();
                hashes = null;

                MemoryStream memStream = new MemoryStream();
                Util.WriteArray(memStream, hashesCopy);

                ZipEntry entry = zipStream.PutNextEntry(Path.Combine(Settings.MetaDataDir, Settings.MetaDataHashes));
                entry.CompressionLevel = Settings.CompressHashes ? (CompressionLevel)Settings.CompressionLevel : CompressionLevel.None;
                memStream.WriteTo(zipStream);
            }

            // Write strings
            {
                MemoryStream memStream = new MemoryStream();
                Util.WriteStream(memStream, strings);

                ZipEntry entry = zipStream.PutNextEntry(Path.Combine(Settings.MetaDataDir, Settings.MetaDataStrings));
                entry.CompressionLevel = (CompressionLevel)Settings.CompressionLevel;
                memStream.WriteTo(zipStream);
            }

            zipStream.Close();

            stats.Status = stats.Canceled ? "Canceled" : "Finished";
            stats.EndTime = DateTime.Now;

            Array.Sort(hashesCopy);
            return new Reference(filename, hashesCopy, strings);
        }
    }
}
