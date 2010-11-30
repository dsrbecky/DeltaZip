using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Ionic.Zip;
using System.Security.Cryptography;
using System.Reflection;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;

namespace DeltaZip
{
    class ArchiveReader
    {
        public string Filename;
        PartialHash[] partialHashes;
        MemoryStream  stringBlob;
        Dictionary<string, ZipEntry> zipEntries = new Dictionary<string, ZipEntry>();

        List<File> files = new List<File>();

        WorkerThread workerThread = new WorkerThread();

        public string FilenameToLower { get { return Filename.ToLowerInvariant(); } }

        public ArchiveReader(string filename, PartialHash[] partialHashes, MemoryStream  stringBlob)
        {
            this.Filename = filename;
            this.partialHashes = partialHashes;
            this.stringBlob = stringBlob;
        }

        public ArchiveReader(string filename, ProgressBar bar, bool loadFiles)
        {
            this.Filename = Path.GetFileName(filename);

            bar.SetStatus("Opening " + Path.GetFileName(filename));

            ZipFile zipFile = new ZipFile(filename);
            foreach (ZipEntry entry in zipFile) {
                zipEntries[entry.FileName.ToLowerInvariant()] = entry;

                // Load file list
                if (entry.FileName == Settings.MetaDataDir + "/" + Settings.MetaDataFiles && loadFiles) {
                    using (MemoryStream memStream = new MemoryStream((int)entry.UncompressedSize)) {
                        entry.Extract(memStream);
                        memStream.Position = 0;
                        files = (List<File>)Util.FileSerializer.Deserialize(new StreamReader(memStream, Encoding.UTF8));
                    }
                    CompressSourceStrings();
                }

                // Load metadata
                if (entry.FileName == Settings.MetaDataDir + "/" + Settings.MetaDataHashes) {
                    using (MemoryStream memStream = new MemoryStream((int)entry.UncompressedSize)) {
                        entry.Extract(memStream);
                        memStream.Position = 0;
                        int sig = Util.ReadInt(memStream);
                        if (sig != Settings.HashFileSignature)
                            throw new Exception("Hash file has incorrect signature");

                        while (true) {
                            int blockType = Util.ReadInt(memStream);

                            if (blockType == 0) {
                                break;
                            } else if (blockType == Settings.HashFileHashBlock) {
                                partialHashes = Util.ReadArray<PartialHash>(memStream);
                            } else if (blockType == Settings.HashFileStringBlock) {
                                stringBlob = Util.ReadStream(memStream);
                            } else {
                                int size = Util.ReadInt(memStream);
                                memStream.Position += size;
                            }
                        }
                    }
                }
            }
        }

        // Save memory by releasing duplicate strings
        public void CompressSourceStrings()
        {
            StringCompressor comp = new StringCompressor();

            foreach (File file in files) {
                comp.Compress(ref file.Name);
                comp.Compress(ref file.SourceArchive);
                foreach (Source source in file.Sources) {
                    comp.Compress(ref source.Archive);
                    comp.Compress(ref source.Path);
                }
            }
        }

        class ExtractedData
        {
            public List<int> Refs = new List<int>(1);
            public ZipEntry SrcEntry;
            public MemoryStream Data;
            public ManualResetEvent LoadDone;

            public static TimeSpan TotalReadTime = TimeSpan.Zero;
            public static double TotalReadSizeMB = 0.0;

            public WaitHandle AsycLoad()
            {
                if (Data == null) {
                    this.Data = StreamPool.Allocate();

                    // Extract
                    if (this.SrcEntry.CompressionMethod == CompressionMethod.Deflate && this.SrcEntry.Encryption == EncryptionAlgorithm.None) {
                        PropertyInfo dataPosProp = typeof(ZipEntry).GetProperty("FileDataPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                        long dataPos = (long)dataPosProp.GetValue(this.SrcEntry, new object[] { });
                        PropertyInfo streamProp = typeof(ZipEntry).GetProperty("ArchiveStream", BindingFlags.NonPublic | BindingFlags.Instance);
                        Stream stream = (Stream)streamProp.GetValue(this.SrcEntry, new object[] { });
                        MemoryStream compressedData = StreamPool.Allocate();
                        compressedData.SetLength(this.SrcEntry.CompressedSize);
                        stream.Seek(dataPos, SeekOrigin.Begin);

                        Stopwatch watch = new Stopwatch();
                        watch.Start();
                        stream.Read(compressedData.GetBuffer(), 0, (int)compressedData.Length);
                        watch.Stop();
                        TotalReadTime += watch.Elapsed;
                        TotalReadSizeMB += (double)compressedData.Length / 1024 / 1024;

                        DeflateStream decompressStream = new System.IO.Compression.DeflateStream(compressedData, CompressionMode.Decompress, true);
                        this.LoadDone = new ManualResetEvent(false);

                        Interlocked.Increment(ref activeDecompressionThreads);
                        ThreadPool.QueueUserWorkItem(delegate {
                            byte[] buffer = new byte[64 * 1024];
                            int readCount;
                            while ((readCount = decompressStream.Read(buffer, 0, buffer.Length)) != 0) {
                                this.Data.Write(buffer, 0, readCount);
                            }
                            decompressStream.Close();
                            StreamPool.Release(ref compressedData);
                            Interlocked.Decrement(ref activeDecompressionThreads);
                            this.LoadDone.Set();
                        });
                    } else {
                        this.SrcEntry.Extract(this.Data);
                        this.LoadDone = new ManualResetEvent(true);
                    }
                }
                return this.LoadDone;
            }

            public override string ToString()
            {
                return string.Format("{0}", this.SrcEntry.FileName);
            }
        }

        static int activeDecompressionThreads = 0;

        public static bool Verify(string archiveFilename, ProgressBar bar)
        {
            ArchiveReader archive = new ArchiveReader(archiveFilename, bar, true);

            Dictionary<string, ArchiveReader> openArchives = new Dictionary<string, ArchiveReader>();
            openArchives[archive.FilenameToLower] = archive;

            // Expand the default sources
            foreach (File file in archive.files) {
                if (file.Sources.Count == 0 && file.Size > 0) {
                    file.Sources.Add(new Source() {
                        Archive = file.SourceArchive,
                        Path = file.Name,
                        Offset = 0,
                        Length = (int)file.Size
                    });
                }
            }

            Dictionary<Source, ExtractedData> getDataCache = new Dictionary<Source, ExtractedData>();

            // Open archives and setup cache
            {
                int time = 0;
                Dictionary<ZipEntry, ExtractedData> getDataCacheForZip = new Dictionary<ZipEntry, ExtractedData>();

                foreach (File file in archive.files) {
                    foreach (Source src in file.Sources) {
                        if (bar.Canceled) return false;

                        // Open the source archive
                        ArchiveReader srcArchive;
                        if (src.Archive == null) {
                            srcArchive = archive;
                        } else {
                            if (!openArchives.TryGetValue(src.Archive.ToLowerInvariant(), out srcArchive)) {
                                srcArchive = new ArchiveReader(Path.Combine(Path.GetDirectoryName(archiveFilename), src.Archive), bar, false);
                                openArchives[src.Archive.ToLowerInvariant()] = srcArchive;
                            }
                        }

                        ZipEntry zipEntry = srcArchive.zipEntries[src.Path.ToLowerInvariant().Replace("\\", "/")];

                        if (!getDataCacheForZip.ContainsKey(zipEntry)) {
                            getDataCacheForZip.Add(zipEntry, new ExtractedData() { SrcEntry = zipEntry } );
                        }
                        getDataCache[src] = getDataCacheForZip[zipEntry];
                        getDataCache[src].Refs.Add(time++);
                    }
                }
            }

            Dictionary<ExtractedData, bool> loaded = new Dictionary<ExtractedData, bool>();
            float waitingForDecompression = 0.0f;
            float mbUnloadedDueToMemoryPressure = 0.0f;

            foreach (File file in archive.files) {
                bar.Progress = 0;
                bar.Filename = file.Name;
                bar.Archive = Path.GetFileName(archiveFilename);
                bar.SetStatus("Verifying " + file.Name);

                SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                file.CheckSourceSizes();

                for (int i = 0; i < file.Sources.Count; i++) {
                    if (bar.Canceled) return false;

                    Source src = file.Sources[i];
                    ExtractedData data = getDataCache[src];

                    // Prefech
                    Dictionary<ExtractedData, bool> prefetched = new Dictionary<ExtractedData, bool>();
                    for (int p = i; p < file.Sources.Count; p++) {
                        ExtractedData pData = getDataCache[file.Sources[p]];
                        pData.AsycLoad();
                        loaded[pData] = true;
                        prefetched[pData] = true;
                        if (prefetched.Count == Settings.ReadPrefetchCount) break;  // We have prefetched enough
                        if (data.LoadDone.WaitOne(TimeSpan.Zero)) break; // Some data is ready - go process it
                    }

                    while(data.LoadDone.WaitOne(TimeSpan.FromSeconds(0.01)) == false) {
                        waitingForDecompression += 0.01f;
                    }

                    // Verify SHA1
                    sha1Provider.TransformBlock(data.Data.GetBuffer(), src.Offset, src.Length, data.Data.GetBuffer(), src.Offset);

                    bar.SetProgress((float)i / (float)file.Sources.Count);

                    // Unload if it is not needed anymore
                    data.Refs.RemoveAt(0);
                    if (data.Refs.Count == 0) {
                        data.LoadDone.WaitOne();
                        StreamPool.Release(ref data.Data);
                        data.LoadDone = null;
                        loaded.Remove(data);
                    }

                    // Unload some data if we are running out of memory
                    while(loaded.Count > Settings.NumCacheLines) {
                        ExtractedData maxRef = null;
                        foreach(ExtractedData ed in loaded.Keys) {
                            if (maxRef == null || ed.Refs[0] > maxRef.Refs[0]) maxRef = ed;
                        }
                        maxRef.LoadDone.WaitOne();
                        mbUnloadedDueToMemoryPressure += (float)maxRef.Data.Length / 1024 / 1024;
                        StreamPool.Release(ref maxRef.Data);
                        maxRef.LoadDone = null;
                        loaded.Remove(maxRef);
                    }
                }
                bar.Progress = 0;

                sha1Provider.TransformFinalBlock(new byte[0], 0, 0);
                byte[] sha1 = sha1Provider.Hash;

                if (new Hash(sha1).CompareTo(new Hash(file.SHA1)) != 0) {
                    return false;
                }
            }
            return true;
        }

        public static bool TryFindSource(Hash hash, List<ArchiveReader> references, Source hintBest, out Source foundSrc)
        {
            // Locate all sources
            List<Source> srcs = new List<Source>();
            foreach (ArchiveReader reference in references) {
                // Does this archive know about the hash?
                int index = Array.BinarySearch(reference.partialHashes, new PartialHash() { Hash = hash });
                if (index < 0) {
                    continue;
                }

                // Extend the selection
                int start = index;
                int end = index;
                while (start - 1 >= 0 && reference.partialHashes[start - 1].Hash.CompareTo(hash) == 0) start--;
                while (end + 1 < reference.partialHashes.Length && reference.partialHashes[end + 1].Hash.CompareTo(hash) == 0) end++;

                // Get the sources
                for (int i = start; i <= end; i++) {
                    PartialHash partialHash = reference.partialHashes[i];
                    // Load the path from the blob
                    reference.stringBlob.Position = partialHash.ZipEntryName;
                    List<byte> nameBytes = new List<byte>();
                    int b;
                    while ((b = reference.stringBlob.ReadByte()) > 0) nameBytes.Add((byte)b);
                    string path = Encoding.UTF8.GetString(nameBytes.ToArray());

                    srcs.Add(new Source() {
                        Archive = reference.Filename,
                        Path = path,
                        Offset = partialHash.Offset,
                        Length = partialHash.Length
                    });
                }
            }

            // Select the best source
            if (srcs.Count == 0) {
                foundSrc = null;
                return false;
            }
            if (hintBest != null) {
                foreach (Source src in srcs) {
                    if (src.Archive == hintBest.Archive &&
                        src.Path == hintBest.Path &&
                        src.Offset == hintBest.Offset + hintBest.Length) {
                        foundSrc = src;
                        return true;
                    }
                }
                foreach (Source src in srcs) {
                    if (src.Path == hintBest.Path && src.Offset == hintBest.Offset + hintBest.Length) {
                        foundSrc = src;
                        return true;
                    }
                }
                foreach (Source src in srcs) {
                    if (src.Path == hintBest.Path) {
                        foundSrc = src;
                        return true;
                    }
                }
            }
            foreach (Source src in srcs) {
                if (src.Offset == 0) {
                    foundSrc = src;
                    return true;
                }
            }
            foundSrc = srcs[0];
            return true;
        }
    }
}
