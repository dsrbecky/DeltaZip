using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using IOFile = System.IO.File;
using System.Windows.Forms;
using System.Threading;

namespace DeltaZip
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Conf.Deserializer.LoadAppConfig<Settings>();

            if (Settings.CreateMulti) Settings.Create = true;

            if (!Settings.Create && !Settings.Extract && !Settings.Verify) {
                Application.Run(new MainForm());
            }

            if (Settings.Create) {
                Create();
            } else if (Settings.Extract) {
                Extract();
            } else if (Settings.Verify) {
                Settings.Dst = null;
                Extract();
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString() + Environment.NewLine + Environment.NewLine + "(press Ctrl+C to copy this message)", "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString() + Environment.NewLine + Environment.NewLine + "(press Ctrl+C to copy this message)", "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void Create()
        {
            ArchiveWriter.Stats stats = new ArchiveWriter.Stats();
            ProgressBar bar = new ProgressBar();
            bar.Bind(stats);

            new Thread((ThreadStart)delegate { Create_Worker(bar, stats); }).Start();

            Application.Run(bar);
        }

        static void Create_Worker(ProgressBar bar, ArchiveWriter.Stats stats)
        {
            string src = Settings.Src;
            string dst = Settings.Dst;
            string refFile = Settings.Ref;

            if (string.IsNullOrEmpty(src)) throw new Exception("You have to specify Src");
            if (string.IsNullOrEmpty(dst)) throw new Exception("You have to specify Dst");

            // The destination is directory
            if (!Settings.CreateMulti && !dst.ToLowerInvariant().EndsWith(Settings.ArchiveExtension.ToLowerInvariant())) {
                dst = Path.Combine(dst, Path.GetFileName(src) + Settings.ArchiveExtension);
            }

            if (Settings.RefRecent) {
                string dir = Path.GetDirectoryName(dst);
                if (Directory.Exists(dir)) {
                    refFile = GetMostRecentArchive(dir);
                }
            }

            Reference reference = null;
            if (!string.IsNullOrEmpty(refFile)) {
                stats.Status = "Opening " + Path.GetFileName(refFile);
                reference = new Reference(refFile);
            }

            List<string> verifycationList = new List<string>();

            if (Settings.CreateMulti) {
                // Multiple archives
                string dstBase = dst;
                foreach (string subDir in Directory.GetDirectories(src)) {
                    dst = Path.Combine(dstBase, Path.GetFileName(subDir)) + Settings.ArchiveExtension;
                    stats.Title = Path.GetFileName(dst);
                    string tmpName = dst + Settings.TmpExtension + new Random().Next();
                    ArchiveWriter archive = new ArchiveWriter(tmpName, stats);
                    archive.AddDir(subDir, reference);
                    reference = archive.Finish(Path.GetFileName(dst));

                    if (stats.Canceled) {
                        IOFile.Delete(tmpName);
                        break;
                    } else {
                        if (IOFile.Exists(dst)) {
                            IOFile.Delete(dst);
                        }
                        IOFile.Move(tmpName, dst);

                        verifycationList.Add(dst);
                    }
                }
            } else {
                stats.Title = Path.GetFileName(dst);
                string tmpName = dst + Settings.TmpExtension + new Random().Next();
                Directory.CreateDirectory(Path.GetDirectoryName(tmpName));
                ArchiveWriter archive = new ArchiveWriter(tmpName, stats);
                archive.AddDir(src, reference);
                archive.Finish(dst);

                if (stats.Canceled) {
                    IOFile.Delete(tmpName);
                } else {
                    if (IOFile.Exists(dst)) {
                        int suffix = 0;
                        string newDst;
                        do {
                            newDst = Path.Combine(Path.GetDirectoryName(dst), Path.GetFileNameWithoutExtension(dst) + (suffix == 0 ? string.Empty : ("-" + suffix.ToString())) + Path.GetExtension(dst));
                            suffix++;
                        } while (IOFile.Exists(newDst));
                        dst = newDst;
                    }
                    IOFile.Move(tmpName, dst);

                    verifycationList.Add(dst);
                }
            }
            stats.EndTime = DateTime.Now;

            // Free memory
            reference = null;

            bool allOk = true;

            if (!stats.Canceled && Settings.Verify) {
                ArchiveReader.Stats verficationStats = new ArchiveReader.Stats();
                bar.Bind(verficationStats);

                foreach (string filename in verifycationList) {
                    allOk = allOk && ArchiveReader.Extract(filename, null, verficationStats);
                }
            }

            if (Settings.AutoQuit && allOk) bar.Close();
        }

        public static string GetMostRecentArchive(string path)
        {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            } else {
                if (Directory.Exists(path)) {
                    path = Path.Combine(path, "*");
                }
                DateTime newestDate = DateTime.MinValue;
                string   newestFilename = string.Empty;
                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path))) {
                    if (string.Equals(Path.GetExtension(file), Settings.ArchiveExtension, StringComparison.InvariantCultureIgnoreCase)) {
                        DateTime date = new FileInfo(file).LastWriteTime;
                        if (date > newestDate) {
                            newestFilename = file;
                            newestDate = date;
                        }
                    }
                }
                return newestFilename;
            }
        }

        public static void Extract()
        {
            ArchiveReader.Stats stats = new ArchiveReader.Stats();
            ProgressBar bar = new ProgressBar();
            bar.Bind(stats);

            string src = GetMostRecentArchive(Settings.Src);

            new Thread((ThreadStart)delegate {
                ArchiveReader.Extract(src, Settings.Dst, stats);
                if (Settings.AutoQuit) bar.Close();
            }).Start();

            Application.Run(bar);
        }
    }
}
