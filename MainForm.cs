using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using IOFile = System.IO.File;
using System.Security.Cryptography;
using System.Threading;

namespace DeltaZip
{
    public partial class MainForm : Form
    {
        static string Filter = "Archive|*" + Settings.ArchiveExtension;

        public MainForm()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString() + Environment.NewLine + Environment.NewLine + "(press Ctrl+C to copy this message)", "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString() + Environment.NewLine + Environment.NewLine + "(press Ctrl+C to copy this message)", "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private void createSrcSelect_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dirDialog = new FolderBrowserDialog();
            if (dirDialog.ShowDialog() == DialogResult.OK) {
                createSrc.Text = dirDialog.SelectedPath;
                RefreshDstDir();
                RefreshAutoReferenceIfNeeded();
            }
        }

        private void createDstSelect_Click(object sender, EventArgs e)
        {
            if (optMulti.Checked) {
                FolderBrowserDialog dirDialog = new FolderBrowserDialog();
                if (dirDialog.ShowDialog() == DialogResult.OK) {
                    createDst.Text = dirDialog.SelectedPath + Path.DirectorySeparatorChar;
                    RefreshAutoReferenceIfNeeded();
                }
            } else {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = Filter;
                if (saveDialog.ShowDialog() == DialogResult.OK) {
                    createDst.Text = saveDialog.FileName;
                    RefreshAutoReferenceIfNeeded();
                }
            }
        }

        private void createRefSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            if (openDlg.ShowDialog() == DialogResult.OK) {
                createRef.Text = openDlg.FileName;
            }
        }

        private void optMulti_CheckedChanged(object sender, EventArgs e)
        {
            RefreshDstDir();
        }

        private void refsAuto_CheckedChanged(object sender, EventArgs e)
        {
            RefreshAutoReferenceIfNeeded();
        }

        void RefreshDstDir()
        {
            if (optMulti.Checked) {
                createDst.Text = createSrc.Text + Path.DirectorySeparatorChar;
            } else {
                createDst.Text = createSrc.Text + Settings.ArchiveExtension;
            }
        }

        void RefreshAutoReferenceIfNeeded()
        {
            if (createRefAuto.Checked) {
                createRef.Text = string.Empty;
                string dst = createDst.Text;
                if (string.IsNullOrEmpty(dst)) return;

                string dir = Path.GetDirectoryName(dst);
                DateTime newest = DateTime.MinValue;
                foreach(string file in Directory.GetFiles(dir, "*" + Settings.ArchiveExtension)) {
                    if (file.ToLowerInvariant() != dst.ToLowerInvariant()) {
                        DateTime date = new FileInfo(file).LastWriteTime;
                        if (date > newest) {
                            createRef.Text = file;
                            newest = date;
                        }
                    }
                }
            }
        }

        private void createBtn_Click(object sender, EventArgs e)
        {
            string src  = createSrc.Text;
            string dst  = createDst.Text;
            string refr = createRef.Text;

            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) return;

            if (IOFile.Exists(dst)) {
                if (!ConfimOverride(dst)) return;
                System.IO.File.Delete(dst);
            }
            new Thread(delegate() {
                CreateDeltaArchive(src, dst, refr);
            }).Start();
        }

        public bool ConfimOverride(string filename)
        {
            string msg = "Override " + filename + "?" + Environment.NewLine + Environment.NewLine + "If any other archives reference data in this archive, you will not be able to extract them.";
            return MessageBox.Show(msg, "File exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public void CreateDeltaArchive(string src, string dst, string refernceFilename)
        {
            ArchiveWriter.Stats stats = new ArchiveWriter.Stats();
            Invoke((MethodInvoker)delegate { new ProgressBar(stats).Show(); });

            Reference reference = null;
            if (!string.IsNullOrEmpty(refernceFilename)) {
                stats.Status = "Opening " + Path.GetFileName(refernceFilename);
                reference = new Reference(refernceFilename);
            }

            List<string> verifycationList = new List<string>();

            if (optMulti.Checked) {
                // Multiple archives
                string dstBase = dst;
                foreach(string subDir in Directory.GetDirectories(src)) {
                    dst = Path.Combine(dstBase, Path.GetFileName(subDir)) + Settings.ArchiveExtension;
                    string tmpName = dst + Settings.TmpExtension;
                    ArchiveWriter archive = new ArchiveWriter(tmpName, stats);
                    archive.AddDir(subDir, reference);
                    reference = archive.Finish(Path.GetFileName(dst));

                    if (stats.Canceled) {
                        IOFile.Delete(tmpName);
                        break;
                    } else {
                        if (IOFile.Exists(dst)) {
                            if (ConfimOverride(dst)) {
                                IOFile.Delete(dst);
                            } else {
                                break;
                            }
                        }
                        IOFile.Move(tmpName, dst);

                        verifycationList.Add(dst);
                    }
                }
            } else {
                string tmpName = dst + Settings.TmpExtension;
                ArchiveWriter archive = new ArchiveWriter(tmpName, stats);
                archive.AddDir(src, reference);
                archive.Finish(dst);

                if (stats.Canceled) {
                    IOFile.Delete(tmpName);
                } else {
                    IOFile.Move(tmpName, dst);

                    verifycationList.Add(dst);
                }
            }
            stats.EndTime = DateTime.Now;

            // Free memory
            reference = null;

            if (!stats.Canceled && optVerify.Checked) {
                foreach (string filename in verifycationList) {
                    if (!ArchiveReader.Extract(filename, null, new ArchiveReader.Stats())) break;
                }
            }
        }

        private void extractSrcSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            if (openDlg.ShowDialog() == DialogResult.OK) {
                extractSrc.Text = openDlg.FileName;
                extractDst.Text = Path.Combine(Path.GetDirectoryName(extractSrc.Text), Path.GetFileNameWithoutExtension(extractSrc.Text));
            }
        }

        private void extractDstSelect_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dirDialog = new FolderBrowserDialog();
            if (dirDialog.ShowDialog() == DialogResult.OK) {
                extractDst.Text = dirDialog.SelectedPath;
            }
        }

        private void extractBtn_Click(object sender, EventArgs e)
        {
            string filename = extractSrc.Text;
            string destination = extractDst.Text;
            new Thread(delegate() {
                Extract(filename, destination);
            }).Start();
        }

        void Extract(string filename, string destination)
        {
            ArchiveReader.Stats stats = new ArchiveReader.Stats();
            Invoke((MethodInvoker)delegate { new ProgressBar(stats).Show(); });

            ArchiveReader.Extract(filename, destination, stats);
        }

        private void verifySrcSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            if (openDlg.ShowDialog() == DialogResult.OK) {
                verifySrc.Text = openDlg.FileName;
            }
        }

        private void verifyBtn_Click(object sender, EventArgs e)
        {
            string filename = verifySrc.Text;
            new Thread(delegate() {
                Verify(filename);
            }).Start();
        }

        void Verify(string filename)
        {
            ArchiveReader.Stats stats = new ArchiveReader.Stats();
            Invoke((MethodInvoker)delegate { new ProgressBar(stats).Show(); });

            ArchiveReader.Extract(filename, null, stats);
        }
    }
}
