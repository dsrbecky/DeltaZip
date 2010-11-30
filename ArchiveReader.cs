using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Ionic.Zip;
using System.Security.Cryptography;
using System.Reflection;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace DeltaZip
{
    public class ArchiveReader
    {
        public string ArchiveName;
        ZipFile       zipFile;
        HashSource[]  hashes;
        MemoryStream  strings;
        List<File>    files;
        Info          info;
        Stats         stats;

        WorkerThread workerThread = new WorkerThread();

        public class Stats
        {
            public string Title;
            public string Status;
            public float Progress;
            public DateTime StartTime = DateTime.Now;
            public DateTime? WriteStartTime = null;
            public DateTime? EndTime;

            public long TotalWritten;
            public long Unmodified;
            public long ReadFromArchiveCompressed;
            public long ReadFromArchiveDecompressed;
            public long ReadFromWorkingCopy;

            public volatile bool Canceled;
        }

        public ArchiveReader(string filename, Stats stats)
        {
            this.ArchiveName = Path.GetFileName(filename);
            this.stats = stats;

            this.stats.Status = "Opening " + ArchiveName;
            this.stats.EndTime = null;

            zipFile = new ZipFile(filename);
            info = (Info)new XmlSerializer(typeof(Info)).Deserialize(Util.ExtractMetaData(zipFile, Settings.MetaDataInfo));

            if (info.ReaderVersion == null) info.ReaderVersion = info.Version;
            if (info.ReaderVersion.Major > Settings.VersionMajor || (info.ReaderVersion.Major == Settings.VersionMajor && info.ReaderVersion.Minor > Settings.VersionMinor)) {
                throw new Exception("The archive was created by newer version of the program: " + info.ReaderVersion);
            }

            hashes  = Util.ReadArray<HashSource>(Util.ExtractMetaData(zipFile, Settings.MetaDataHashes));
            strings = Util.ReadStream(Util.ExtractMetaData(zipFile, Settings.MetaDataStrings));
            files   = (List<File>)Util.FileSerializer.Deserialize(new StreamReader(Util.ExtractMetaData(zipFile, Settings.MetaDataFiles), Encoding.UTF8));
        }

        public string GetString(int index)
        {
            strings.Position = index;
            List<byte> nameBytes = new List<byte>();
            int b;
            while ((b = strings.ReadByte()) > 0) nameBytes.Add((byte)b);
            return Encoding.UTF8.GetString(nameBytes.ToArray());
        }

        class MemoryStreamRef
        {
            public WaitHandle Ready;
            public Hash Hash;
            public MemoryStream MemStream;
            public long Offset;
            public int Length;

            public ExtractedData CacheLine;
        }

        class ExtractedData
        {
            public List<int> Refs = new List<int>(1);
            public MemoryStream Data;
            public ManualResetEvent LoadDone;

            public static TimeSpan TotalReadTime = TimeSpan.Zero;
            public static double TotalReadSizeMB = 0.0;

            public WaitHandle AsycDecompress(ZipEntry srcEntry)
            {
                if (Data == null) {
                    if (StreamPool.Capacity < srcEntry.UncompressedSize) StreamPool.Capacity = (int)srcEntry.UncompressedSize;
                    this.Data = StreamPool.Allocate();

                    // Extract
                    if (srcEntry.CompressionMethod == CompressionMethod.Deflate && srcEntry.Encryption == EncryptionAlgorithm.None) {
                        PropertyInfo dataPosProp = typeof(ZipEntry).GetProperty("FileDataPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                        long dataPos = (long)dataPosProp.GetValue(srcEntry, new object[] { });
                        PropertyInfo streamProp = typeof(ZipEntry).GetProperty("ArchiveStream", BindingFlags.NonPublic | BindingFlags.Instance);
                        Stream stream = (Stream)streamProp.GetValue(srcEntry, new object[] { });
                        MemoryStream compressedData = StreamPool.Allocate();
                        compressedData.SetLength(srcEntry.CompressedSize);
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
                        srcEntry.Extract(this.Data);
                        this.LoadDone = new ManualResetEvent(true);
                    }
                }
                return this.LoadDone;
            }
        }

        static int activeDecompressionThreads = 0;

        public static bool Extract(string archiveFilename, string destination, Stats stats)
        {
            ArchiveReader archive = new ArchiveReader(archiveFilename, stats);

            bool writeEnabled = (destination != null);

            Dictionary<string, ZipFile> openZips = new Dictionary<string, ZipFile>();
            openZips[archive.ArchiveName.ToLowerInvariant()] = archive.zipFile;

            FileStream   openFile = null;
            IAsyncResult openFileRead = null;
            string       openFilePathLC = null;

            // Setup cache
            Dictionary<string, ExtractedData> dataCache = new Dictionary<string, ExtractedData>();
            int time = 0;
            foreach (File file in archive.files) {
                foreach (int hashIndex in file.HashIndices) {
                    if (stats.Canceled) return false;

                    string path = archive.GetString(archive.hashes[hashIndex].Path).ToLowerInvariant();
                    if (!dataCache.ContainsKey(path)) dataCache.Add(path, new ExtractedData());
                    dataCache[path].Refs.Add(time++);
                }
            }

            string stateFile = Path.Combine(destination, Settings.StateFile);

            stats.Status = "Loading working copy state";
            WorkingCopy workingCopy = writeEnabled ? WorkingCopy.Load(stateFile) : new WorkingCopy();
            List<WorkingFile> newWorkingFiles = new List<WorkingFile>();
            List<WorkingHash> oldWorkingHashes = new List<WorkingHash>();
            workingCopy = WorkingCopy.HashLocalFiles(destination, stats, workingCopy);
            foreach(WorkingFile wf in workingCopy.Files) {
                foreach(WorkingHash wh in wf.Hashes) {
                    wh.File = wf;
                }
                oldWorkingHashes.AddRange(wf.Hashes);
            }
            oldWorkingHashes.Sort();

            // Save the result of the hashing work
            stats.Status = "Saving working copy state";
            workingCopy.Save(stateFile);

            string tmpPath = null;
            if (writeEnabled) {
                tmpPath = Path.Combine(destination, ".deltazip");
                Directory.CreateDirectory(tmpPath);
            }

            Dictionary<ExtractedData, bool> loaded = new Dictionary<ExtractedData, bool>();
            float waitingForDecompression = 0.0f;
            float mbUnloadedDueToMemoryPressure = 0.0f;

            stats.Status = writeEnabled ? "Extracting" : "Verifying";
            stats.WriteStartTime = DateTime.Now;

            foreach (File file in archive.files) {

                string tmpFileName = null;
                FileStream outFile = null;             

                if (writeEnabled) {

                    // Quickpath - see if the file exists and has correct content
                    WorkingFile workingFile = workingCopy.Files.Find(f => f.NameLowercase == Path.Combine(destination, file.Name).ToLowerInvariant());
                    if (workingFile != null && workingFile.ExistsOnDisk() && !workingFile.IsModifiedOnDisk()) {
                        if (new Hash(workingFile.Hash).CompareTo(new Hash(file.Hash)) == 0) {
                            // The file is already there - no need to extract it
                            newWorkingFiles.Add(workingFile);
                            stats.Unmodified += file.Size;
                            continue;
                        }
                    }

                    int tmpFileNamePostfix = 0;
                    do {
                        tmpFileName = Path.Combine(tmpPath, file.Name + (tmpFileNamePostfix == 0 ? string.Empty : ("-" + tmpFileNamePostfix.ToString())));
                        tmpFileNamePostfix++;
                    } while (System.IO.File.Exists(tmpFileName));

                    Directory.CreateDirectory(Path.GetDirectoryName(tmpFileName));
                    outFile = new FileStream(tmpFileName, FileMode.CreateNew, FileAccess.Write);
                    // Avoid fragmentation
                    outFile.SetLength(file.Size);
                    outFile.Position = 0;
                }

                List<WorkingHash> workingHashes = new List<WorkingHash>();

                try {
                    stats.Progress = 0;
                    stats.Title = Path.GetFileName(file.Name);
                    stats.Status = (writeEnabled ? "Extracting " : "Verifying ") + file.Name;

                    SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                    Queue<MemoryStreamRef> writeQueue = new Queue<MemoryStreamRef>();

                    int p = 0;
                    for (int i = 0; i < file.HashIndices.Count; i++) {
                        if (stats.Canceled) {
                            stats.Status = "Canceled.  No files were modified.";
                            return false;
                        }

                        // Prefetch
                        for (; p < file.HashIndices.Count; p++) {
                            int prefetchSize = 0;
                            Dictionary<MemoryStream, bool> prefetchedStreams = new Dictionary<MemoryStream, bool>();
                            foreach(MemoryStreamRef memStreamRef in writeQueue) {
                                prefetchedStreams[memStreamRef.MemStream] = true;
                            }
                            foreach(MemoryStream prefetchedStream in prefetchedStreams.Keys) {
                                prefetchSize += (int)prefetchedStream.Length;
                            }
                            if (writeQueue.Count > 0 && prefetchSize > Settings.WritePrefetchSize) break;  // We have prefetched enough data

                            if (writeQueue.Count > 0 && writeQueue.Peek().Ready.WaitOne(TimeSpan.Zero)) break; // Some data is ready - go process it

                            HashSource hashSrc = archive.hashes[file.HashIndices[p]];
                            string path = archive.GetString(hashSrc.Path).ToLowerInvariant();
                            ExtractedData data = dataCache[path];

                            // See if we have the hash on disk.  Try our best not to seek too much
                            WorkingHash onDiskHash = null;
                            long bestSeekDistance = long.MaxValue;
                            int idx = oldWorkingHashes.BinarySearch(new WorkingHash() { Hash = hashSrc.Hash });
                            if (idx >= 0) {
                                while (idx - 1 >= 0 && oldWorkingHashes[idx - 1].Hash.Equals(hashSrc.Hash)) idx--;
                                for (; idx < oldWorkingHashes.Count && oldWorkingHashes[idx].Hash.Equals(hashSrc.Hash); idx++) {
                                    WorkingHash wh = oldWorkingHashes[idx];
                                    long seekDistance;
                                    if (openFile != null && openFilePathLC == wh.File.NameLowercase) {
                                        seekDistance = Math.Abs(openFile.Position - wh.Offset);
                                    } else {
                                        seekDistance = long.MaxValue;
                                    }
                                    if (onDiskHash == null || seekDistance < bestSeekDistance) {
                                        onDiskHash = wh;
                                        bestSeekDistance = seekDistance;
                                    }
                                }
                            }

                            if (onDiskHash != null && ((openFilePathLC == onDiskHash.File.NameLowercase) || (onDiskHash.File.ExistsOnDisk() && !onDiskHash.File.IsModifiedOnDisk()))) {
                                MemoryStream memStream = new MemoryStream(onDiskHash.Length);
                                memStream.SetLength(onDiskHash.Length);
                                // Finish the last read
                                if (openFileRead != null) {
                                    openFile.EndRead(openFileRead);
                                    openFileRead = null;
                                }
                                // Open other file
                                if (openFilePathLC != onDiskHash.File.NameLowercase) {
                                    if (openFile != null) openFile.Dispose();
                                    openFile = new FileStream(onDiskHash.File.NameLowercase, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileStreamBufferSize, FileOptions.None);
                                    openFilePathLC = onDiskHash.File.NameLowercase;
                                    System.Diagnostics.Debug.Write(Path.GetFileName(onDiskHash.File.NameMixedcase));
                                }
                                System.Diagnostics.Debug.Write(onDiskHash.Offset == openFile.Position ? "." : "S");
                                if (openFile.Position != onDiskHash.Offset)
                                    openFile.Position = onDiskHash.Offset;
                                openFileRead = openFile.BeginRead(memStream.GetBuffer(), 0, (int)memStream.Length, null, null);
                                writeQueue.Enqueue(new MemoryStreamRef() {
                                    Ready     = openFileRead.AsyncWaitHandle,
                                    MemStream = memStream,
                                    Offset    = 0,
                                    Length    = (int)memStream.Length,
                                    CacheLine = null,
                                    Hash      = hashSrc.Hash
                                });
                                stats.ReadFromWorkingCopy += hashSrc.Length;
                            } else {
                                // Locate and load the zipentry
                                ZipEntry pZipEntry;
                                path = path.Replace("\\", "/");
                                if (path.StartsWith("/")) {
                                    pZipEntry = archive.zipFile[path.Substring(1)];
                                } else {
                                    int slashIndex = path.IndexOf("/");
                                    string zipPath = path.Substring(0, slashIndex);
                                    string entryPath = path.Substring(slashIndex + 1);
                                    if (!openZips.ContainsKey(zipPath)) openZips[zipPath] = new ZipFile(Path.Combine(Path.GetDirectoryName(archiveFilename), zipPath));
                                    pZipEntry = openZips[zipPath][entryPath];
                                }

                                if (data.Data == null) {
                                    stats.ReadFromArchiveDecompressed += pZipEntry.UncompressedSize;
                                    stats.ReadFromArchiveCompressed   += pZipEntry.CompressedSize;
                                    data.AsycDecompress(pZipEntry);
                                }
                                loaded[data] = true;

                                writeQueue.Enqueue(new MemoryStreamRef() {
                                    Ready     = data.LoadDone,
                                    MemStream = data.Data,
                                    Offset    = hashSrc.Offset,
                                    Length    = hashSrc.Length,
                                    CacheLine = data,
                                    Hash      = hashSrc.Hash
                                });
                            }
                        }

                        MemoryStreamRef writeItem = writeQueue.Dequeue();

                        while (writeItem.Ready.WaitOne(TimeSpan.FromSeconds(0.01)) == false) {
                            waitingForDecompression += 0.01f;
                        }

                        // Write output
                        if (writeEnabled) {
                            workingHashes.Add(new WorkingHash() {
                                Hash = writeItem.Hash,
                                Offset = outFile.Position,
                                Length = writeItem.Length
                            });

                            outFile.Write(writeItem.MemStream.GetBuffer(), (int)writeItem.Offset, writeItem.Length);
                        }

                        // Verify SHA1
                        sha1Provider.TransformBlock(writeItem.MemStream.GetBuffer(), (int)writeItem.Offset, writeItem.Length, writeItem.MemStream.GetBuffer(), (int)writeItem.Offset);

                        stats.TotalWritten += writeItem.Length;

                        stats.Progress = (float)i / (float)file.HashIndices.Count;

                        // Unload if it is not needed anymore
                        if (writeItem.CacheLine != null) {
                            writeItem.CacheLine.Refs.RemoveAt(0);
                            if (writeItem.CacheLine.Refs.Count == 0) {
                                StreamPool.Release(ref writeItem.CacheLine.Data);
                                writeItem.CacheLine.LoadDone = null;
                                loaded.Remove(writeItem.CacheLine);
                            }
                        }

                        // Unload some data if we are running out of memory
                        while (loaded.Count * Settings.MaxZipEntrySize > Settings.WriteCacheSize) {
                            ExtractedData maxRef = null;
                            foreach (ExtractedData ed in loaded.Keys) {
                                if (maxRef == null || ed.Refs[0] > maxRef.Refs[0]) maxRef = ed;
                            }
                            maxRef.LoadDone.WaitOne();

                            // Check that we are not evicting something from the write queue
                            bool inQueue = false;
                            foreach(MemoryStreamRef memRef in writeQueue) {
                                if (memRef.CacheLine == maxRef) inQueue = true;
                            }
                            if (inQueue) break;

                            mbUnloadedDueToMemoryPressure += (float)maxRef.Data.Length / 1024 / 1024;
                            StreamPool.Release(ref maxRef.Data);
                            maxRef.LoadDone = null;
                            loaded.Remove(maxRef);
                        }
                    }
                    stats.Progress = 0;

                    sha1Provider.TransformFinalBlock(new byte[0], 0, 0);
                    byte[] sha1 = sha1Provider.Hash;

                    if (new Hash(sha1).CompareTo(new Hash(file.Hash)) != 0) {
                        MessageBox.Show("The checksum of " + file.Name + " does not match original value.  The file is corrupted.", "Critical error", MessageBoxButtons.OK);
                        if (writeEnabled) {
                            stats.Status = "Extraction failed.  Checksum mismatch.";
                        } else {
                            stats.Status = "Verification failed.  Checksum mismatch.";
                        }
                        return false;
                    }

                } finally {
                    if (outFile != null) outFile.Dispose();
                }

                if (writeEnabled) {
                    FileInfo fileInfo = new FileInfo(tmpFileName);

                    WorkingFile workingFile = new WorkingFile() {
                        NameMixedcase = Path.Combine(destination, file.Name),
                        Size = fileInfo.Length,
                        Created = fileInfo.CreationTime,
                        Modified = fileInfo.LastWriteTime,
                        Hash = file.Hash,
                        TempFileName = tmpFileName,
                        Hashes = workingHashes
                    };
                    newWorkingFiles.Add(workingFile);
                }
            }

            stats.Progress = 0;

            // Close sources
            foreach (ZipFile zip in openZips.Values) {
                zip.Dispose();
            }
            if (openFileRead != null) openFile.EndRead(openFileRead);
            if (openFile != null)     openFile.Dispose();

            // Replace the old working copy with new one
            if (writeEnabled) {
                List<string> deleteFilesLC = new List<string>();
                List<string> deleteFilesAskLC = new List<string>();
                List<string> keepFilesLC = new List<string>();

                stats.Status = "Looking for local modifications";

                // Delete all non-modified files which are not part of the new set
                foreach (WorkingFile workingFile in workingCopy.Files) {
                    if (!workingFile.UserModified && !newWorkingFiles.Contains(workingFile)) {
                        deleteFilesLC.Add(workingFile.NameLowercase);
                    }
                }

                // Find obstructions for new files
                foreach (WorkingFile newWorkingFile in newWorkingFiles) {
                    if (newWorkingFile.TempFileName != null && newWorkingFile.ExistsOnDisk() && !deleteFilesLC.Contains(newWorkingFile.NameLowercase)) {
                        deleteFilesAskLC.Add(newWorkingFile.NameLowercase);
                    }
                }

                // Ask the user for permission to delete
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Do you want to override local changes in the following files?");
                foreach (string deleteFileAskLC in deleteFilesAskLC) {
                    sb.AppendLine(deleteFileAskLC);
                }
                if (deleteFilesAskLC.Count > 0) {
                    DialogResult overrideAnswer = MessageBox.Show(sb.ToString(), "Override files", MessageBoxButtons.YesNoCancel);
                    if (overrideAnswer == DialogResult.Cancel) {
                        stats.Status = "Canceled.  No files were modified.";
                        return false;
                    }
                    if (overrideAnswer == DialogResult.Yes) {
                        deleteFilesLC.AddRange(deleteFilesAskLC);
                    } else {
                        keepFilesLC = deleteFilesAskLC;
                    }
                    deleteFilesAskLC.Clear();
                }

                // Delete files
                foreach (string deleteFileLC in deleteFilesLC) {
                    stats.Status = "Deleting " + Path.GetFileName(deleteFileLC);
                    while (true) {
                        try {
                            System.IO.File.Delete(deleteFileLC);
                            workingCopy.Files.RemoveAll(f => f.NameLowercase == deleteFileLC);
                            break;
                        } catch (Exception e) {
                            DialogResult deleteAnswer = MessageBox.Show("Can not delete file " + deleteFileLC + Environment.NewLine + e.Message, "Error", MessageBoxButtons.AbortRetryIgnore);
                            if (deleteAnswer == DialogResult.Retry) continue;
                            if (deleteAnswer == DialogResult.Ignore) break;
                            if (deleteAnswer == DialogResult.Abort) {
                                stats.Status = "Canceled.  Some files were deleted.";
                                return false;
                            }
                        }
                    }
                }

                // Move the new files
                foreach (WorkingFile newWorkingFile in newWorkingFiles) {
                    if (!keepFilesLC.Contains(newWorkingFile.NameLowercase) && newWorkingFile.TempFileName != null) {
                        stats.Status = "Moving " + Path.GetFileName(newWorkingFile.NameMixedcase);
                        while (true) {
                            try {
                                Directory.CreateDirectory(Path.GetDirectoryName(newWorkingFile.NameMixedcase));
                                System.IO.File.Move(newWorkingFile.TempFileName, newWorkingFile.NameMixedcase);
                                workingCopy.Files.Add(newWorkingFile);
                                break;
                            } catch (Exception e) {
                                DialogResult moveAnswer = MessageBox.Show("Error when moving " + newWorkingFile.TempFileName + Environment.NewLine + e.Message, "Error", MessageBoxButtons.AbortRetryIgnore);
                                if (moveAnswer == DialogResult.Retry) continue;
                                if (moveAnswer == DialogResult.Ignore) break;
                                if (moveAnswer == DialogResult.Abort) {
                                    stats.Status = "Canceled.  Some files were deleted or overridden.";
                                    return false;
                                }
                            }
                        }
                    }
                }

                stats.Status = "Saving working copy state";
                workingCopy.Save(stateFile);

                stats.Status = "Deleting temporary directory";
                try {
                    if (Directory.Exists(tmpPath)) Directory.Delete(tmpPath, true);
                } catch {
                }
            }

            stats.EndTime = DateTime.Now;
            if (writeEnabled) {
                stats.Status = "Extraction finished";
            } else {
                stats.Status = "Verification finished";
            }
            return true;
        }
    }
}
