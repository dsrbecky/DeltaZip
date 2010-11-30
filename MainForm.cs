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
                RefreshAutoReferencesIfNeeded();
            }
        }

        private void createDstSelect_Click(object sender, EventArgs e)
        {
            if (optMulti.Checked) {
                FolderBrowserDialog dirDialog = new FolderBrowserDialog();
                if (dirDialog.ShowDialog() == DialogResult.OK) {
                    createDst.Text = dirDialog.SelectedPath + Path.DirectorySeparatorChar;
                    RefreshAutoReferencesIfNeeded();
                }
            } else {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = Filter;
                if (saveDialog.ShowDialog() == DialogResult.OK) {
                    createDst.Text = saveDialog.FileName;
                    RefreshAutoReferencesIfNeeded();
                }
            }
        }

        private void optMulti_CheckedChanged(object sender, EventArgs e)
        {
            RefreshDstDir();
        }

        void RefreshDstDir()
        {
            if (optMulti.Checked) {
                createDst.Text = createSrc.Text + Path.DirectorySeparatorChar;
            } else {
                createDst.Text = createSrc.Text + Settings.ArchiveExtension;
            }
        }

        private void refsAuto_CheckedChanged(object sender, EventArgs e)
        {
            RefreshAutoReferencesIfNeeded();
        }

        void RefreshAutoReferencesIfNeeded()
        {
            if (refsAuto.Checked) {
                refs.Items.Clear();
                string dst = createDst.Text;
                if (string.IsNullOrEmpty(dst)) return;

                string dir = Path.GetDirectoryName(dst);
                foreach(string file in Directory.GetFiles(dir, "*" + Settings.ArchiveExtension)) {
                    if (file.ToLowerInvariant() != dst.ToLowerInvariant()) {
                        refs.Items.Add(file);
                    }
                }
            }
        }

        private void refsAdd_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            openDlg.Multiselect = true;
            if (openDlg.ShowDialog() == DialogResult.OK) {
                refs.Items.AddRange(openDlg.FileNames);
            }
            refsAuto.Checked = false;
        }

        private void refsRemove_Click(object sender, EventArgs e)
        {
            while(refs.SelectedIndex != -1)
                refs.Items.RemoveAt(refs.SelectedIndex);
            refsAuto.Checked = false;
        }

        private void createBtn_Click(object sender, EventArgs e)
        {
            string src = createSrc.Text;
            string dst = createDst.Text;

            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) return;

            List<string> refs = new List<string>();
            foreach(string s in this.refs.Items) {
                refs.Add(s);
            }
            if (IOFile.Exists(dst)) {
                if (!ConfimOverride(dst)) return;
            }
            new Thread(delegate() {
                CreateDeltaArchive(src, dst, refs);
            }).Start();
        }

        public bool ConfimOverride(string filename)
        {
            string msg = "Override " + filename + "?" + Environment.NewLine + Environment.NewLine + "If any other archives reference data in this archive, you will not be able to extract them.";
            return MessageBox.Show(msg, "File exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public void CreateDeltaArchive(string src, string dst, List<string> refernceFilenames)
        {
            ProgressBar bar = new ProgressBar();
            Invoke((MethodInvoker)delegate { bar.Show(); });
            bar.Archive = Path.GetFileName(dst);

            List<ArchiveReader> references = new List<ArchiveReader>();
            foreach(string refernceFilename in refernceFilenames) {
                references.Add(new ArchiveReader(refernceFilename, bar, false));
            }

            List<string> verifycationList = new List<string>();

            if (optMulti.Checked) {
                // Multiple archives
                string dstBase = dst;
                foreach(string subDir in Directory.GetDirectories(src)) {
                    dst = Path.Combine(dstBase, Path.GetFileName(subDir)) + Settings.ArchiveExtension;
                    bar.Archive = Path.GetFileName(dst);
                    string tmpName = dst + Settings.TmpExtension;
                    ArchiveWriter archive = new ArchiveWriter(tmpName, bar);
                    // TODO: Find relative path
                    archive.AddDir(subDir, references);
                    references.Add(archive.Finish(Path.GetFileName(dst)));

                    if (bar.Canceled) {
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
                ArchiveWriter archive = new ArchiveWriter(tmpName, bar);
                archive.AddDir(src, references);
                archive.Finish(dst);

                if (bar.Canceled) {
                    IOFile.Delete(tmpName);
                } else {
                    if (IOFile.Exists(dst))
                        IOFile.Delete(dst);
                    IOFile.Move(tmpName, dst);

                    verifycationList.Add(dst);
                }
            }

            // Free memory
            references = null;

            if (bar.Canceled) {
                bar.SetStatus("Canceled");
            } else {
                if (optVerify.Checked) {
                    bool allOk = true;
                    foreach(string filename in verifycationList) {
                        allOk = allOk && ArchiveReader.Verify(filename, bar);
                    }
                    if (allOk) {
                        bar.SetStatus("Finished and verified");
                    } else {
                        if (bar.Canceled) {
                            bar.SetStatus("Finished but verification was canceled");
                        } else {
                            bar.SetStatus("Verification failed!");
                            MessageBox.Show("Verification failed!", "Verification", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                } else {
                    bar.SetStatus("Finished");
                }
            }

            bar.DisableCancelButton();
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
            MessageBox.Show("Not implemented");
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
            ProgressBar bar = new ProgressBar();
            bar.Archive = Path.GetFileName(filename);
            Invoke((MethodInvoker)delegate { bar.Show(); });

            bool ok = ArchiveReader.Verify(filename, bar);
            if (ok) {
                bar.SetStatus("Verification successful");
            } else {
                if (bar.Canceled) {
                    bar.SetStatus("Verification canceled");
                } else {
                    bar.SetStatus("Verification failed!");
                    MessageBox.Show("Verification failed!", "Verification", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
