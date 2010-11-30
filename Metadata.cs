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
}
