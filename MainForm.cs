using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
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

            if (!string.IsNullOrEmpty(Settings.DefaultExtractSrc)) {
                tabControl1.SelectTab(tabPage2);
                extractSrc.Text = Settings.DefaultExtractSrc;
                // Select the newest file
                if (Directory.Exists(Settings.DefaultExtractSrc)) {
                    DateTime newest = DateTime.MinValue;
                    foreach (string file in Directory.GetFiles(Settings.DefaultExtractSrc, "*" + Settings.ArchiveExtension)) {
                        DateTime date = new FileInfo(file).LastWriteTime;
                        if (date > newest) {
                            extractSrc.Text = file;
                            newest = date;
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(Settings.DefaultExtractDst)) {
                extractDst.Text = Settings.DefaultExtractDst;
            }
        }

        private void createSrcSelect_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dirDialog = new FolderBrowserDialog();
            if (dirDialog.ShowDialog() == DialogResult.OK) {
                createSrc.Text = dirDialog.SelectedPath;
                if (string.IsNullOrEmpty(createDst.Text)) {
                    RefreshDstDir();
                }
            }
        }

        private void createDstSelect_Click(object sender, EventArgs e)
        {
            if (optMulti.Checked) {
                FolderBrowserDialog dirDialog = new FolderBrowserDialog();
                if (dirDialog.ShowDialog() == DialogResult.OK) {
                    createDst.Text = dirDialog.SelectedPath + Path.DirectorySeparatorChar;
                }
            } else {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = Filter;
                if (saveDialog.ShowDialog() == DialogResult.OK) {
                    createDst.Text = saveDialog.FileName;
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

        void RefreshDstDir()
        {
            if (optMulti.Checked) {
                createDst.Text = createSrc.Text + Path.DirectorySeparatorChar;
            } else {
                createDst.Text = createSrc.Text + Settings.ArchiveExtension;
            }
        }

        private void createBtn_Click(object sender, EventArgs e)
        {
            Settings.Create = true;
            Settings.Src = createSrc.Text;
            Settings.Dst = createDst.Text;
            Settings.Ref = createRef.Text;
            Settings.RefRecent = createRefAuto.Checked;
            Settings.Verify = optVerify.Checked;
            Settings.CreateMulti = optMulti.Checked;

            if (string.IsNullOrEmpty(Settings.Src) || string.IsNullOrEmpty(Settings.Dst)) return;

            this.Close();
        }

        void extractSrcSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            if (openDlg.ShowDialog() == DialogResult.OK) {
                extractSrc.Text = openDlg.FileName;
                if (string.IsNullOrEmpty(extractDst.Text)) {
                    extractDst.Text = Path.Combine(Path.GetDirectoryName(extractSrc.Text), Path.GetFileNameWithoutExtension(extractSrc.Text));
                }
            }
        }

        void extractDstSelect_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dirDialog = new FolderBrowserDialog();
            if (dirDialog.ShowDialog() == DialogResult.OK) {
                extractDst.Text = dirDialog.SelectedPath;
            }
        }

        void extractBtn_Click(object sender, EventArgs e)
        {
            Settings.Extract = true;
            Settings.Src = extractSrc.Text;
            Settings.Dst = extractDst.Text;
            this.Close();
        }

        void verifySrcSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            if (openDlg.ShowDialog() == DialogResult.OK) {
                verifySrc.Text = openDlg.FileName;
            }
        }

        void verifyBtn_Click(object sender, EventArgs e)
        {
            Settings.Verify = true;
            Settings.Src = verifySrc.Text;
            Settings.Dst = null;
            this.Close();
        }
    }
}
