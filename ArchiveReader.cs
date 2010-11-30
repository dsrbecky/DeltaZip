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
using System.Windows.Forms;

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

            public Directory<Hash, > LocalData;

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

        public static bool Extract(string archiveFilename, string destination, ProgressBar bar)
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

            bar.SetStatus("Loading working copy state");
            WorkingCopy oldWorkingCopy = WorkingCopy.Load();
            WorkingCopy newWorkingCopy = new WorkingCopy();

            bar.SetStatus("Looking data that you already have");
            foreach(WorkingFile workingFile in oldWorkingCopy.Files) {
                foreach(WorkingHash workingHash in workingFile.Hashes) {

                }
            }

            Dictionary<ExtractedData, bool> loaded = new Dictionary<ExtractedData, bool>();
            float waitingForDecompression = 0.0f;
            float mbUnloadedDueToMemoryPressure = 0.0f;

            bool writeEnabled = (destination != null);

            string tmpPath = Path.Combine(destination, ".deltazip");

            bar.SetStatus(writeEnabled ? "Extracting" : "Verifying");

            foreach (File file in archive.files) {

                string tmpFileName = null;
                FileStream outFile = null;

                // Quickpath - see if the file exists and has correct content
                // Should there be file name liked that on disk?
                WorkingFile oldWorkingFile = oldWorkingCopy.Files.Find(f => f.NameLowercase == Path.Combine(destination, file.Name).ToLowerInvariant());
                if (oldWorkingFile != null && oldWorkingFile.ExistsOnDisk() && !oldWorkingFile.IsModifiedOnDisk()) {
                    // The file is already there - no need to extract it
                    oldWorkingCopy.Files.Remove(oldWorkingFile);
                    newWorkingCopy.Files.Add(oldWorkingFile);
                    continue;
                }

                if (writeEnabled) {
                    Directory.CreateDirectory(tmpPath);

                    int tmpFileNamePostfix = 0;
                    do {
                        tmpFileName = Path.Combine(tmpPath, file.Name + (tmpFileNamePostfix == 0 ? string.Empty : ("-" + tmpFileNamePostfix.ToString())));
                        tmpFileNamePostfix++;
                    } while (System.IO.File.Exists(tmpFileName));

                    outFile = new FileStream(tmpFileName, FileMode.CreateNew, FileAccess.Write);
                }

                List<WorkingHash> workingHashes = new List<WorkingHash>();

                try {
                    bar.Progress = 0;
                    bar.Filename = file.Name;
                    bar.Archive = Path.GetFileName(archiveFilename);
                    bar.SetStatus((writeEnabled ? "Extracting " : "Verifying ") + file.Name);

                    SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                    file.CheckSourceSizes();

                    for (int i = 0; i < file.Sources.Count; i++) {
                        if (bar.Canceled) {
                            if (outFile != null) outFile.Dispose();
                            bar.SetStatus("Canceled.  No files were modified.");
                            return false;
                        }

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

                        while (data.LoadDone.WaitOne(TimeSpan.FromSeconds(0.01)) == false) {
                            waitingForDecompression += 0.01f;
                        }

                        // Verify SHA1
                        sha1Provider.TransformBlock(data.Data.GetBuffer(), src.Offset, src.Length, data.Data.GetBuffer(), src.Offset);

                        // Write output
                        if (writeEnabled) {
                            workingHashes.Add(new WorkingHash() {
                                Hash = Hash.Compute(data.Data.GetBuffer(), src.Offset, src.Length),
                                Offset = outFile.Position,
                                Length = src.Length
                            });

                            outFile.Write(data.Data.GetBuffer(), src.Offset, src.Length);
                        }

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
                        while (loaded.Count > Settings.NumCacheLines) {
                            ExtractedData maxRef = null;
                            foreach (ExtractedData ed in loaded.Keys) {
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
                        MessageBox.Show("The checksum of " + file.Name + " does not match original value.  The file is corrupted.", "Critical error", MessageBoxButtons.OK);
                        if (writeEnabled) {
                            bar.SetStatus("Extraction failed.  Checksum mismatch.");
                        } else {
                            bar.SetStatus("Verification failed.  Checksum mismatch.");
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
                        SHA1 = file.SHA1,
                        TempFileName = tmpFileName,
                        Hashes = workingHashes
                    };
                    newWorkingCopy.Files.Add(workingFile);
                }
            }

            bar.SetProgress(0);

            // Remplace the old working copy with new one
            if (writeEnabled) {
                List<string> deleteFilesLC = new List<string>();
                List<string> deleteFilesAskLC = new List<string>();
                List<string> keepFilesLC = new List<string>();

                bar.SetStatus("Looking for local modifications");

                // Delete old working copy
                foreach (WorkingFile workingFile in oldWorkingCopy.Files) {
                    if (workingFile.ExistsOnDisk()) {
                        if (workingFile.IsModifiedOnDisk()) {
                            deleteFilesAskLC.Add(workingFile.NameLowercase);
                        } else {
                            deleteFilesLC.Add(workingFile.NameLowercase);
                        }
                    }
                }

                // Find obstructions for new working copy
                foreach (WorkingFile workingFile in newWorkingCopy.Files) {
                    if (workingFile.TempFileName != null &&
                        workingFile.ExistsOnDisk() &&
                        !deleteFilesLC.Contains(workingFile.NameLowercase) &&
                        !deleteFilesAskLC.Contains(workingFile.NameLowercase))
                    {
                        deleteFilesAskLC.Add(workingFile.NameLowercase);
                    }
                }

                // Ask the user for permission to delete
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Do you want to override local changes in the following files?");
                foreach (string deleteFileAskLC in deleteFilesAskLC) {
                    sb.AppendLine(deleteFileAskLC);
                }
                DialogResult overrideAnswer = MessageBox.Show(sb.ToString(), "Override files", MessageBoxButtons.YesNoCancel);
                if (overrideAnswer == DialogResult.Cancel) {
                    bar.SetStatus("Canceled.  No files were modified.");
                    return false;
                }
                if (overrideAnswer == DialogResult.Yes) {
                    deleteFilesLC.AddRange(deleteFilesAskLC);
                } else {
                    keepFilesLC = deleteFilesAskLC;
                }
                deleteFilesAskLC.Clear();

                // Delete files
                foreach(string deleteFile in deleteFilesLC) {
                    bar.SetStatus("Deleting " + Path.GetFileName(deleteFile));
                    while(true) {
                        try {
                            System.IO.File.Delete(deleteFile);
                        } catch (Exception e) {
                            DialogResult deleteAnswer = MessageBox.Show("Can not delete file " + deleteFile + Environment.NewLine + e.Message, "Error", MessageBoxButtons.AbortRetryIgnore);
                            if (deleteAnswer == DialogResult.Retry)  continue;
                            if (deleteAnswer == DialogResult.Ignore) break;
                            if (deleteAnswer == DialogResult.Abort) {
                                bar.SetStatus("Canceled.  Some files were deleted.");
                                return false;
                            }
                        }
                    }
                }

                // Move the new files
                foreach (WorkingFile workingFile in newWorkingCopy.Files) {
                    bar.SetStatus("Moving " + Path.GetFileName(workingFile.NameLowercase));
                    if (!keepFilesLC.Contains(workingFile.NameLowercase)) {
                        while(true) {
                            try {
                                Directory.CreateDirectory(Path.GetDirectoryName(workingFile.NameLowercase));
                                System.IO.File.Move(workingFile.TempFileName, workingFile.NameLowercase);
                            } catch (Exception e) {
                                DialogResult moveAnswer = MessageBox.Show("Error when moving " + workingFile.TempFileName + Environment.NewLine + e.Message, "Error", MessageBoxButtons.AbortRetryIgnore);
                                if (moveAnswer == DialogResult.Retry)  continue;
                                if (moveAnswer == DialogResult.Ignore) break;
                                if (moveAnswer == DialogResult.Abort) {
                                    bar.SetStatus("Canceled.  Some files were deleted or overridden.");
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            bar.SetStatus("Deleting temporary directory");
            try {
                if (Directory.Exists(tmpPath)) Directory.Delete(tmpPath, true);
            } catch {
            }

            bar.SetStatus("Saving working copy state");
            newWorkingCopy.Save();

            if (writeEnabled) {
                bar.SetStatus("Extraction finished");
            } else {
                bar.SetStatus("Verification finished");
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
