using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Ionic.Zlib;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.Xml.Serialization;
using Ionic.Zip;

namespace DeltaZip
{
    static class Util
    {
        static XmlSerializer fileSerializer;

        public static XmlSerializer FileSerializer {
            get {
                if (fileSerializer == null) {
                    fileSerializer = new XmlSerializer(typeof(List<File>), new XmlRootAttribute("Files"));
                }
                return fileSerializer;
            }
        }

        static XmlSerializer workingCopySerializer;

        public static XmlSerializer WorkingCopySerializer {
            get {
                if (workingCopySerializer == null) {
                    workingCopySerializer = new XmlSerializer(typeof(WorkingCopy), new XmlRootAttribute("Files"));
                }
                return workingCopySerializer;
            }
        }

        // Is it worth compressing the data?
        public static bool IsCompressable(MemoryStream stream)
        {
            if (Settings.CompressionLevel == 0)
                return false;

            if (stream.Length < 8 * Settings.CompressableTestSkip)
                return true;

            long inputSize = 0;
            MemoryStream deflated = new MemoryStream();
            DeflateStream defStream = new DeflateStream(deflated, CompressionMode.Compress, (CompressionLevel)Settings.CompressionLevel, true);
            for (int i = 0; i + Settings.CompressableTestSize < stream.Length ;i += Settings.CompressableTestSkip) {
                inputSize += Settings.CompressableTestSize;
                defStream.Write(stream.GetBuffer(), i, Settings.CompressableTestSize);
            }
            defStream.Close();

            float compression = (float)deflated.Length / (float)(inputSize + 1);
            return compression <= Settings.CompressionTreshold;
        }

        public static string BytesToStr(byte[] bytes)
        {
            StringBuilder str = new StringBuilder();
            foreach (byte b in bytes)
                str.AppendFormat("{0:X2}", b);
            return str.ToString();
        }

        public static unsafe void WriteInt(MemoryStream dst, int i)
        {
            dst.Position = dst.Length;
            dst.SetLength(dst.Length + sizeof(int));
            Marshal.Copy(new IntPtr(&i), dst.GetBuffer(), (int)dst.Position, sizeof(int));
            dst.Position = dst.Length;
        }

        public static unsafe void WriteArray<T>(MemoryStream dst, T[] array) where T : struct
        {
            int size = array.Length * Marshal.SizeOf(typeof(T));
            WriteInt(dst, size);
            dst.Position = dst.Length;
            dst.SetLength(dst.Length + size);
            GCHandle gch = GCHandle.Alloc(array, GCHandleType.Pinned);
            IntPtr ptrArray = gch.AddrOfPinnedObject();
            Marshal.Copy(ptrArray, dst.GetBuffer(), (int)dst.Position, size);
            dst.Position = dst.Length;
            gch.Free();
        }

        public static unsafe void WriteStream(MemoryStream dst, MemoryStream src)
        {
            WriteInt(dst, (int)src.Length);
            src.WriteTo(dst);
        }

        public static unsafe int ReadInt(MemoryStream src)
        {
            int i;
            Marshal.Copy(src.GetBuffer(), (int)src.Position, new IntPtr(&i), sizeof(int));
            src.Position += sizeof(int);
            return i;
        }

        public static unsafe T[] ReadArray<T>(MemoryStream src)
        {
            int size = ReadInt(src);
            int count = size / Marshal.SizeOf(typeof(T));
            if (size % Marshal.SizeOf(typeof(T)) != 0)
                throw new Exception("Total size must be multiple of array element size");
            T[] array = new T[count];
            GCHandle gch = GCHandle.Alloc(array, GCHandleType.Pinned);
            IntPtr ptrArray = gch.AddrOfPinnedObject();
            Marshal.Copy(src.GetBuffer(), (int)src.Position, ptrArray, size);
            gch.Free();
            src.Position += size;
            return array;
        }

        public static MemoryStream ReadStream(MemoryStream src)
        {
            int size = ReadInt(src);
            MemoryStream dst = new MemoryStream(size);
            dst.SetLength(size);
            Array.Copy(src.GetBuffer(), src.Position, dst.GetBuffer(), 0, size);
            src.Position += size;
            return dst;
        }

        public static MemoryStream ExtractMetaData(ZipFile zipFile, string metadata)
        {
            ZipEntry zipEntry = zipFile[Settings.MetaDataDir + "/" + metadata];
            MemoryStream stream = new MemoryStream((int)zipEntry.UncompressedSize);
            zipEntry.Extract(stream);
            stream.Position = 0;
            return stream;
        }
    }

    /// <summary>
    /// Save memory by releasing duplicate strings from memory
    /// </summary>
    class StringCompressor
    {
        Dictionary<string, string> dict = new Dictionary<string, string>();

        public string Compress(string str)
        {
            if (str == null) {
                return null;
            } else {
                string oldOne;
                if (dict.TryGetValue(str, out oldOne)) {
                    return oldOne;
                } else {
                    dict.Add(str, str);
                    return str;
                }
            }
        }

        public void Compress(ref string str)
        {
            str = Compress(str);
        }
    }

    class WorkerThread
    {
        // Pulse it when content changes
        Queue<MethodInvoker> pendingFlushes = new Queue<MethodInvoker>();
        Thread processingThread;
        bool exit = false;

        public void Enqueue(MethodInvoker method)
        {
            if (Settings.AsyncWrite) {
                // Asynchronous compression
                lock (pendingFlushes) {
                    while (pendingFlushes.Count >= Settings.MaxQueuedWrites) {
                        Monitor.Wait(pendingFlushes);
                    }
                    pendingFlushes.Enqueue(method);
                    Monitor.PulseAll(pendingFlushes);
                }

                if (processingThread == null) {
                    processingThread = new Thread(ProcessFlushes);
                    processingThread.Name = "WorkerThread";
                    processingThread.Start();
                }
            }
            else {
                method();
            }
        }

        void ProcessFlushes()
        {
            while (!exit) {
                MethodInvoker next;
                lock (pendingFlushes) {
                    if (pendingFlushes.Count > 0) {
                        next = pendingFlushes.Dequeue();
                        Monitor.PulseAll(pendingFlushes);
                    }
                    else {
                        Monitor.Wait(pendingFlushes, TimeSpan.FromSeconds(1));
                        continue;
                    }
                }
                next();
            }
        }

        public void WaitUntilDoneAndExit()
        {
            ManualResetEvent done = new ManualResetEvent(false);
            Enqueue((MethodInvoker)delegate { done.Set(); Thread.Sleep(0); });
            done.WaitOne();
            exit = true;
        }
    }

    static class StreamPool
    {
        static Queue<WeakReference> free = new Queue<WeakReference>();
        static object syncObject = new object();

        static public int Capacity = Settings.MaxZipEntrySize;

        public static MemoryStream Allocate()
        {
            lock(syncObject) {
                while(free.Count > 0) {
                    WeakReference weakRef = free.Dequeue();
                    MemoryStream stream = (MemoryStream)weakRef.Target;
                    if (stream != null) {
                        stream.Position = 0;
                        stream.SetLength(0);
                        return stream;
                    }
                }
                return new MemoryStream(Capacity);
            }
        }

        public static void Release(ref MemoryStream stream)
        {
            lock(syncObject) {
                free.Enqueue(new WeakReference(stream));
                stream = null;
            }
        }
    }
}
