using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Ionic.Zip;
using Ionic.Zlib;
using System.Threading;

namespace DeltaZip
{
    public class File
    {
        [XmlAttribute] public string Name;
        [XmlAttribute] public long Size;
        [XmlAttribute(DataType = "hexBinary")] public byte[] SHA1;
        [XmlAttribute] public DateTime Created;
        [XmlAttribute] public DateTime Modified;
        [XmlAttribute] public FileAttributes Attributes;
        [XmlAttribute] public string SourceArchive;

        [XmlElement(ElementName="Source")]
        public List<Source> Sources = new List<Source>();

        public void CheckSourceSizes()
        {
            if (this.Sources.Count > 0) {
                long size = 0;
                foreach(Source src in this.Sources) {
                    size += src.Length;
                }
                if (size != Size) {
                    throw new Exception("Total size of sources does not match file size");
                }
            }
        }
    }

    public class Source
    {
        [XmlAttribute] public string Archive;
        [XmlAttribute] public string Path;
        [XmlAttribute] public int Offset;
        [XmlAttribute] public int Length;

        public override string ToString()
        {
            return string.Format("{0}/{1} ({2}+{3})", Archive, Path, Offset, Length);
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
    }

    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct PartialHash: IComparable<PartialHash>
    {
        public Hash Hash;
        public int  ZipEntryName;
        public int  Offset;
        public int  Length;

        public int CompareTo(PartialHash other)
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
        [XmlAttribute(DataType = "hexBinary")] public byte[] SHA1;

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

    public class WorkingHash
    {
        [XmlIgnore]    public Hash Hash;
        [XmlAttribute] public long Offset;
        [XmlAttribute] public int Length;
        [XmlAttribute(DataType = "hexBinary")]
        public byte[] SHA1
        {
            get { return this.Hash.ToByteArray(); }
            set { this.Hash = new Hash(value); }
        }
    }

    public class WorkingCopy
    {
        [XmlElement(ElementName = "File")]
        public List<WorkingFile> Files = new List<WorkingFile>();

        static string StoragePath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(Path.Combine(appData, "DeltaZip"), "WorkingCopy.xml");
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StoragePath));
            using (FileStream writter = new FileStream(StoragePath, FileMode.Create)) {
                Util.WorkingCopySerializer.Serialize(writter, this);
            }
        }

        public static WorkingCopy Load()
        {
            if (System.IO.File.Exists(StoragePath)) {
                try {
                    using (FileStream reader = new FileStream(StoragePath, FileMode.Open)) {
                        return (WorkingCopy)Util.WorkingCopySerializer.Deserialize(reader);
                    }
                }
                catch {
                    return new WorkingCopy();
                }
            }
            else {
                return new WorkingCopy();
            }
        }

        /*
        public static WorkingCopy HashLocalFiles(string path, WorkingCopy lastWorkingCopy)
        {
            WorkingCopy wc = new WorkingCopy();

            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (string filename in files)
            {
                FileInfo fileInfo = new FileInfo(filename);

                WorkingFile lastFile = lastWorkingCopy.Files.Find(file => file.Name == filename);

                if (lastFile           != null &&
                    lastFile.Size      == fileInfo.Length &&
                    lastFile.Created   == fileInfo.CreationTime &&
                    lastFile.Modified  == fileInfo.LastWriteTime) {
                    // Do not rehash - assume it was unchanged
                    wc.Files.Add(lastFile);
                } else {
                    using (FileStream fileStreamIn = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileStreamBufferSize, true)) {
                        SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider();

                        WorkingFile file = new WorkingFile() {
                            Name = filename,
                            Size = fileInfo.Length,
                            Created = fileInfo.CreationTime,
                            Modified = fileInfo.LastWriteTime,
                            UserFile = true
                        };

                        long offset = 0;
                        foreach (Block block in Splitter.Split(fileStreamIn, sha1Provider)) {
                            WorkingHash hash = new WorkingHash() {
                                Hash   = Hash.Compute(block),
                                Length = block.Length,
                                Offset = offset
                            };
                            file.Hashes.Add(hash);
                            offset += block.Length;
                        };
                        file.SHA1 = sha1Provider.Hash;
                    }
                }
            }

            return wc;
        }
        */
    }
}
