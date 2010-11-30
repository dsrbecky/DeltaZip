using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Ionic.Zip;
using Ionic.Zlib;
using System.Threading;
using System.Text.RegularExpressions;

namespace DeltaZip
{
    public class Version
    {
        [XmlAttribute] public int Major;
        [XmlAttribute] public int Minor;

        public override string ToString()
        {
            return Major.ToString() + "." + Minor.ToString();
        }
    }

    public class Info
    {
        public Version  Version;
        public Version  ReaderVersion;
        public string   CreatedBy;
        public DateTime CreatedOn;
        public string   Comment;
    }

    public class File
    {
        [XmlAttribute] public string Name;
        [XmlAttribute] public long Size;
        [XmlAttribute(DataType = "hexBinary")] public byte[] Hash;
        [XmlAttribute] public DateTime Created;
        [XmlAttribute] public DateTime Modified;
        [XmlAttribute] public FileAttributes Attributes;
        [XmlIgnore]    public List<int> HashIndices = new List<int>();
        [XmlText]      public string HashIndicesAsText {
            get {
                const int wrap = 200;
                int lastWrap = 0;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < this.HashIndices.Count; i++) {
                    sb.Append(this.HashIndices[i].ToString());
                    bool consecutive = false;
                    while (i + 1 < this.HashIndices.Count && this.HashIndices[i+1] == this.HashIndices[i] + 1) {
                        consecutive = true;
                        i++;
                    }
                    if (consecutive) {
                        sb.Append('-');
                        sb.Append(this.HashIndices[i].ToString());
                    }
                    if (i != this.HashIndices.Count - 1) {
                        if (sb.Length - lastWrap >= wrap) {
                            sb.Append(Environment.NewLine);
                            lastWrap = sb.Length;
                        } else {
                            sb.Append(' ');
                        }
                    }
                }
                if (sb.Length > 50) {
                    return Environment.NewLine + sb.ToString();
                } else {
                    return sb.ToString();
                }
            }
            set {
                string[] indices = Regex.Split(value, @"\s+");
                this.HashIndices = new List<int>(indices.Length);
                foreach(string index in indices) {
                    if (!string.IsNullOrEmpty(index)) {
                        if (index.Contains("-")) {
                            string[] range = index.Split('-');
                            int start = int.Parse(range[0]);
                            int end   = int.Parse(range[1]);
                            for(int i = start; i <= end; i++) {
                                this.HashIndices.Add(i);
                            }
                        } else {
                            this.HashIndices.Add(int.Parse(index));
                        }
                    }
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Hash: IComparable<Hash>
    {
        public uint Part0;
        public uint Part1;
        public uint Part2;
        public uint Part3;
        public uint Part4;

        public static Hash Compute(Block block)
        {
            SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();
            byte[] sha1 = sha1Provider.ComputeHash(block.Buffer, block.Offset, block.Length);
            return new Hash(sha1);
        }

        public static Hash Compute(byte[] buffer, int offset, int length)
        {
            SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();
            byte[] sha1 = sha1Provider.ComputeHash(buffer, offset, length);
            return new Hash(sha1);
        }

        public Hash(byte[] sha1)
        {
            unchecked {
                this.Part0 = (uint)((sha1[0] << 24) | (sha1[1] << 16) | (sha1[2] << 8) | (sha1[3]));
                this.Part1 = (uint)((sha1[4] << 24) | (sha1[5] << 16) | (sha1[6] << 8) | (sha1[7]));
                this.Part2 = (uint)((sha1[8] << 24) | (sha1[9] << 16) | (sha1[10] << 8) | (sha1[11]));
                this.Part3 = (uint)((sha1[12] << 24) | (sha1[13] << 16) | (sha1[14] << 8) | (sha1[15]));
                this.Part4 = (uint)((sha1[16] << 24) | (sha1[17] << 16) | (sha1[18] << 8) | (sha1[19]));
            }
        }

        public byte[] ToByteArray()
        {
            unchecked {
                return new byte[] {
                    (byte)(this.Part0 >> 24), (byte)(this.Part0 >> 16), (byte)(this.Part0 >> 8), (byte)(this.Part0),
                    (byte)(this.Part1 >> 24), (byte)(this.Part1 >> 16), (byte)(this.Part1 >> 8), (byte)(this.Part1),
                    (byte)(this.Part2 >> 24), (byte)(this.Part2 >> 16), (byte)(this.Part2 >> 8), (byte)(this.Part2),
                    (byte)(this.Part3 >> 24), (byte)(this.Part3 >> 16), (byte)(this.Part3 >> 8), (byte)(this.Part3),
                    (byte)(this.Part4 >> 24), (byte)(this.Part4 >> 16), (byte)(this.Part4 >> 8), (byte)(this.Part4),
                };
            }
        }

        public int CompareTo(Hash other)
        {
            if (this.Part0 != other.Part0) return this.Part0.CompareTo(other.Part0);
            if (this.Part1 != other.Part1) return this.Part1.CompareTo(other.Part1);
            if (this.Part2 != other.Part2) return this.Part2.CompareTo(other.Part2);
            if (this.Part3 != other.Part3) return this.Part3.CompareTo(other.Part3);
            if (this.Part4 != other.Part4) return this.Part4.CompareTo(other.Part4);
            return 0;
        }

        public bool Equals(Hash obj)
        {
            return CompareTo(obj) == 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct HashSource: IComparable<HashSource>
    {
        public Hash Hash;
        public int  Path;
        public long Offset;
        public int  Length;

        public int CompareTo(HashSource other)
        {
            return this.Hash.CompareTo(other.Hash);
        }
    }

    public class WorkingFile
    {
        [XmlIgnore]            public string NameLowercase { get { return this.NameMixedcase.ToLowerInvariant(); } }
        [XmlAttribute("Name")] public string NameMixedcase;

        [XmlAttribute] public long Size;
        [XmlAttribute] public DateTime Created;
        [XmlAttribute] public DateTime Modified;
        [XmlAttribute("SHA1", DataType = "hexBinary")] public byte[] Hash;
        [XmlAttribute] public bool UserModified;

        [XmlElement(ElementName = "Hash")]
        public List<WorkingHash> Hashes = new List<WorkingHash>();

        [XmlIgnore] public string TempFileName;

        public bool ExistsOnDisk()
        {
            return System.IO.File.Exists(NameLowercase);
        }

        public bool IsModifiedOnDisk()
        {
            FileInfo fi = new FileInfo(NameLowercase);
            return (fi.Length != Size || fi.CreationTime != Created || fi.LastWriteTime != Modified);
        }
    }

    public class WorkingHash : IComparable<WorkingHash>
    {
        [XmlIgnore]    public WorkingFile File;
        [XmlIgnore]    public Hash Hash;
        [XmlAttribute] public long Offset;
        [XmlAttribute] public int  Length;
        [XmlAttribute(DataType = "hexBinary")]
        public byte[] SHA1
        {
            get { return this.Hash.ToByteArray(); }
            set { this.Hash = new Hash(value); }
        }

        public int CompareTo(WorkingHash other)
        {
            return this.Hash.CompareTo(other.Hash);
        }
    }

    /// <summary>
    /// This tries to mirror the state of the disk with the additional feature
    /// of knowing the hashes and the 'user modified' flag
    /// </summary>
    public class WorkingCopy
    {
        [XmlElement(ElementName = "File")]
        Dictionary<string, WorkingFile> Files = new Dictionary<string, WorkingFile>();

        public int Count { get { return Files.Count; } }
        
        public void Add(WorkingFile wf)
        {
            Files.Add(wf.NameLowercase, wf);
        }

        public void Remove(string path)
        {
            Files.Remove(path.ToLowerInvariant());
        }

        public bool Contains(string path)
        {
            return Files.ContainsKey(path.ToLowerInvariant());
        }

        public WorkingFile Find(string path)
        {
            WorkingFile wf;
            if (Files.TryGetValue(path.ToLowerInvariant(), out wf)) return wf;
            return null;
        }

        public IEnumerable<WorkingFile> GetAll()
        {
            return Files.Values;
        }

        public static WorkingCopy Load(string path)
        {
            if (System.IO.File.Exists(path)) {
                try {
                    using (FileStream reader = new FileStream(path, FileMode.Open)) {
                        using (Stream deflate = new System.IO.Compression.DeflateStream(reader, System.IO.Compression.CompressionMode.Decompress, true)) {
                            List<WorkingFile> list = (List<WorkingFile>)Util.WorkingCopySerializer.Deserialize(deflate);
                            WorkingCopy wc = new WorkingCopy();
                            foreach (WorkingFile wf in list) {
                                wc.Add(wf);
                            }
                            return wc;
                        }
                    }
                } catch {
                    return new WorkingCopy();
                }
            } else {
                return new WorkingCopy();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            List<WorkingFile> list = new List<WorkingFile>();
            foreach (WorkingFile wf in Files.Values) {
                list.Add(wf);
            }
            using (FileStream writter = new FileStream(path, FileMode.Create)) {
                using (Stream deflate = new System.IO.Compression.DeflateStream(writter, System.IO.Compression.CompressionMode.Compress, true)) {
                    Util.WorkingCopySerializer.Serialize(deflate, list);
                }
            }
        }

        public static WorkingCopy HashLocalFiles(string path, ArchiveReader.Stats stats, WorkingCopy lastWorkingCopy)
        {
            WorkingCopy wc = new WorkingCopy();

            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (string filename in files) {
                FileInfo fileInfo = new FileInfo(filename);

                if (filename.ToLowerInvariant() == Path.Combine(path, Settings.StateFile).ToLowerInvariant())
                    continue;

                if (filename.ToLowerInvariant().StartsWith(Path.Combine(path, Settings.TmpDirectory).ToLowerInvariant()))
                    continue;

                WorkingFile lastFile = lastWorkingCopy.Find(filename);

                stats.Status = "Checking " + Path.GetFileName(filename);

                if (lastFile != null && !lastFile.IsModifiedOnDisk()) {
                    wc.Add(lastFile);
                } else {
                    stats.Status = "Hashing local file " + Path.GetFileName(filename);
                    using (FileStream fileStreamIn = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileStreamBufferSize, true)) {
                        SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                        WorkingFile file = new WorkingFile() {
                            NameMixedcase = filename,
                            Size = fileInfo.Length,
                            Created = fileInfo.CreationTime,
                            Modified = fileInfo.LastWriteTime,
                            UserModified = true
                        };

                        List<WaitHandle> hashOps = new List<WaitHandle>();

                        long offset = 0;
                        foreach (Block block in Splitter.Split(fileStreamIn, sha1Provider, true)) {
                            if (stats.Canceled) {
                                stats.Canceled = false;
                                stats.EndTime  = null;
                                return lastWorkingCopy;
                            }
                            WorkingHash hash = new WorkingHash() {
                                Length = block.Length,
                                Offset = offset
                            };
                            ManualResetEvent done = new ManualResetEvent(false);
                            hashOps.Add(done);
                            Block blockCopy = block;
                            ThreadPool.QueueUserWorkItem(delegate {
                                hash.Hash = Hash.Compute(blockCopy);
                                done.Set();
                            });
                            file.Hashes.Add(hash);
                            offset += block.Length;
                            stats.Progress = (float)offset / (float)file.Size;
                        };

                        foreach (WaitHandle hashOp in hashOps) hashOp.WaitOne();

                        file.Hash = sha1Provider.Hash;
                        wc.Add(file);
                    }
                }
            }

            return wc;
        }
    }
}
