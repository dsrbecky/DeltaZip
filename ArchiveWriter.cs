using System;
using System.Collections.Generic;
using System.Linq;
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
    class ArchiveWriter
    {
        ProgressBar bar;
        FileStream baseStream;
        ZipOutputStream zipStream;

        List<File> files = new List<File>();

        WriteBuffer writeBuffer;
        Dictionary<Hash, Source> localBlocks = new Dictionary<Hash, Source>();

        List<PartialHash>       partialHashes = new List<PartialHash>();
        MemoryStream            stringBlob = new MemoryStream();
        Dictionary<string, int> stringBlobIndex = new Dictionary<string, int>();

        public ArchiveWriter(string filename, ProgressBar bar)
        {
            this.bar = bar;
            this.baseStream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            this.zipStream = new ZipOutputStream(this.baseStream);
            this.zipStream.EnableZip64 = Zip64Option.AsNecessary;
            this.zipStream.ParallelDeflateThreshold = 0; // Force enabled
            this.zipStream.UseUnicodeAsNecessary = true;
            this.writeBuffer = new WriteBuffer(this);
        }

        public void AddDir(string path, List<ArchiveReader> references)
        {
            bar.SetStatus("Reading directory " + path + "...");
            path = Path.GetFullPath(path);
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            // Strip path to reduce memory
            for (int i = 0; i < files.Length; i++) {
                if (files[i].StartsWith(path)) {
                    files[i] = files[i].Substring(path.Length).TrimStart('\\');
                }
            }

            List<string> exclude = Settings.Exclude.Select(s => s.ToLowerInvariant()).ToList();

            for (int i = 0; i < files.Length; i++) {
                if (bar.Canceled) return;

                if (exclude.Contains(Path.GetFileName(files[i]).ToLowerInvariant())) {
                    continue;
                }
                
                AddFile(path, files[i], references);
            }
        }

        public void AddFile(string root, string filename, List<ArchiveReader> references)
        {
            string fullFilename = Path.Combine(root, filename);
            using (FileStream fileStreamIn = new FileStream(fullFilename, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileStreamBufferSize, true)) {
                bar.Filename = filename;
                bar.Progress = 0;
                bar.SetStatus("Compressing " + filename);

                writeBuffer.Name = filename;
                writeBuffer.NameSuffix = 0;
                writeBuffer.ForceAppendSuffix = false;

                SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                // Store
                Source hintBest = new Source() { Path = filename };
                List<Source> sources = new List<Source>();
                long offset = 0;
                foreach (Block block in Splitter.Split(fileStreamIn, sha1Provider)) {
                    if (bar.Canceled) return;

                    Hash hash = Hash.Compute(block);

                    Source src;
                    if (Settings.AllowLocalReferences && localBlocks.TryGetValue(hash, out src)) {
                        // Internal reference
                        bar.SavedByInternalDelta += block.Length;
                        writeBuffer.ForceAppendSuffix = true;
                    } else if (ArchiveReader.TryFindSource(hash, references, hintBest, out src)) {
                        // Reference to existing archive
                        bar.SavedByExternalDelta += block.Length;
                        writeBuffer.ForceAppendSuffix = true;
                        // Consider further references as local
                        localBlocks[hash] = src;
                    } else {
                        // Store the data locally
                        src = writeBuffer.Write(hash, block);
                        localBlocks[hash] = src;
                    }
                    sources.Add(src);
                    hintBest = src;

                    offset += block.Length;

                    bar.Progress = (float)offset / (float)fileStreamIn.Length;
                };
                if (offset != fileStreamIn.Length)
                    throw new Exception("Internal consistency error");

                writeBuffer.Flush();

                FileInfo fileInfo = new FileInfo(fullFilename);

                File file = new File() {
                    Name = filename,
                    Size = fileInfo.Length,
                    SHA1 = sha1Provider.Hash,
                    Created = fileInfo.CreationTime,
                    Modified = fileInfo.LastWriteTime,
                    Attributes = fileInfo.Attributes & ~Settings.IgnoreAttributes,
                    SourceArchive = null,
                    Sources = sources
                };
                files.Add(file);

                file.CheckSourceSizes();

                CompressFile(file);

                file.CheckSourceSizes();
            }
        }

        void CompressFile(File file)
        {
            // Merge consecutive sources
            List<Source> res = new List<Source>();
            foreach (Source src in file.Sources) {
                if (res.Count == 0) {
                    res.Add(src);
                } else {
                    Source last = res[res.Count - 1];
                    if (src.Archive == last.Archive &&
                        src.Path == last.Path &&
                        src.Offset == last.Offset + last.Length) {
                        // Merge sources
                        // Create new source - the old one might be used at multiple locations
                        res.RemoveAt(res.Count - 1);
                        res.Add(new Source() {
                            Archive = last.Archive,
                            Path = last.Path,
                            Offset = last.Offset,
                            Length = last.Length + src.Length
                        });
                    } else {
                        res.Add(src);
                    }
                }
            }
            file.Sources = res;

            // Don't write default source
            if (file.Sources.Count == 1 &&
                file.Sources[0].Path == file.Name &&
                file.Sources[0].Offset == 0 &&
                file.Sources[0].Length == file.Size) {
                file.SourceArchive = file.Sources[0].Archive;
                file.Sources.Clear();
            }

            file.Sources.TrimExcess();
        }

        class WriteBuffer
        {
            ArchiveWriter archive;

            public string Name;
            public int    NameSuffix = 0;
            public bool   ForceAppendSuffix;

            MemoryStream      Content = StreamPool.Allocate();
            List<Source>      Sources = new List<Source>();
            List<PartialHash> Hashes = new List<PartialHash>();

            WorkerThread workerThread = new WorkerThread();

            public WriteBuffer(ArchiveWriter archive)
            {
                this.archive = archive;
            }

            public Source Write(Hash hash, Block block)
            {
                if (Content.Length + block.Length > Settings.MaxZipEntrySize) {
                    Flush(false);
                }

                Source src = new Source() {
                    Offset = (int)Content.Length,
                    Length = block.Length
                };
                Sources.Add(src);

                PartialHash row = new PartialHash() {
                    Hash = hash,
                    Offset = (int)Content.Length,
                    Length = block.Length,
                };
                Hashes.Add(row);

                Content.Write(block.Buffer, block.Offset, block.Length);
                
                return src;
            }

            public void Flush()
            {
                Flush(true);
            }

            void Flush(bool final)
            {
                if (Sources.Count > 0) {
                    // Name the enity
                    string nameWithSuffix = (final && NameSuffix == 0 && !ForceAppendSuffix) ? Name : (Name + "~" + NameSuffix.ToString("D3"));
                    NameSuffix++;

                    // Content of entity
                    MemoryStream entityContent = this.Content;

                    // Allocate new tmp stream
                    this.Content = StreamPool.Allocate();

                    // Output sources
                    foreach (Source src in Sources) {
                        src.Path = nameWithSuffix;
                    }
                    Sources.Clear();

                    // Output hashes
                    foreach (PartialHash ph in Hashes) {
                        int index = 0;
                        if (!archive.stringBlobIndex.TryGetValue(nameWithSuffix, out index)) {
                            index = (int)archive.stringBlob.Position;
                            archive.stringBlobIndex[nameWithSuffix] = index;
                            byte[] stringData = Encoding.UTF8.GetBytes(nameWithSuffix);
                            archive.stringBlob.Write(stringData, 0, stringData.Length);
                            archive.stringBlob.WriteByte(0);
                        }
                        PartialHash phCopy = ph;
                        phCopy.ZipEntryName = index;
                        archive.partialHashes.Add(phCopy);
                    }
                    Hashes.Clear();

                    MethodInvoker writeMethod = delegate {
                        bool compress = Util.IsCompressable(entityContent);
                        long oldCompressedSize = archive.baseStream.Position;

                        archive.zipStream.ParallelDeflateThreshold = entityContent.Length >= Settings.MinSizeForParallelDeflate ? 0 /* on */ : -1 /* off */;
                        ZipEntry entry = archive.zipStream.PutNextEntry(nameWithSuffix);
                        entry.CompressionLevel = compress ? (CompressionLevel)Settings.Compression : CompressionLevel.None;
                        entityContent.Position = 0;
                        entityContent.WriteTo(archive.zipStream);
                        archive.zipStream.Flush();

                        long compressedSize = archive.baseStream.Position - oldCompressedSize;
                        archive.bar.Compressed += compressedSize;
                        archive.bar.SavedByCompression += entityContent.Length - compressedSize;
                        archive.bar.Refresh();

                        StreamPool.Release(ref entityContent);
                    };

                    workerThread.Enqueue(writeMethod);
                }
            }

            public void WaitUntilDoneAndExit()
            {
                workerThread.WaitUntilDoneAndExit();
            }
        }

        public unsafe ArchiveReader Finish(string filename)
        {
            // Release memory
            writeBuffer.WaitUntilDoneAndExit();
            writeBuffer = null;
            localBlocks = new Dictionary<Hash, Source>();
            stringBlobIndex = new Dictionary<string, int>();

            bar.Filename = "";
            bar.SetStatus("Writing metadata");

            // Write file list
            {
                ZipEntry entry = zipStream.PutNextEntry(Path.Combine(Settings.MetaDataDir, Settings.MetaDataFiles));
                entry.CompressionLevel = (CompressionLevel)Settings.Compression;
                Util.FileSerializer.Serialize(new StreamWriter(zipStream, Encoding.UTF8), files);

                // Release memory
                files = new List<File>();
            }

            // Write metadata
            PartialHash[] partialHashesCopy;
            {
                // Sort
                partialHashes.Sort();
                partialHashesCopy = partialHashes.ToArray();
                partialHashes = null;

                MemoryStream memStream = new MemoryStream(256 + partialHashesCopy.Length * sizeof(PartialHash) + (int)stringBlob.Length);
                Util.WriteInt(memStream, Settings.HashFileSignature);
                Util.WriteInt(memStream, Settings.HashFileHashBlock);
                Util.WriteArray(memStream, partialHashesCopy);
                Util.WriteInt(memStream, Settings.HashFileStringBlock);
                Util.WriteStream(memStream, stringBlob);
                Util.WriteInt(memStream, 0);

                ZipEntry entry = zipStream.PutNextEntry(Path.Combine(Settings.MetaDataDir, Settings.MetaDataHashes));
                entry.CompressionLevel = Settings.CompressHashes ? (CompressionLevel)Settings.Compression : CompressionLevel.None;
                memStream.WriteTo(zipStream);
            }

            zipStream.Close();

            bar.SetStatus("Done");

            return new ArchiveReader(filename, partialHashesCopy, stringBlob);
        }
    }
}
