using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DeltaZip
{
    public partial class ProgressBar : Form
    {
        public volatile bool Canceled;

        public string Archive;
        public string Filename;
        public float Progress;
        public string Status;

        public long Compressed;
        public long SavedByCompression;
        public long SavedByInternalDelta;
        public long SavedByExternalDelta;

        public ProgressBar()
        {
            InitializeComponent();
        }

        public void SetStatus(string status)
        {
            Status = status;
            Refresh();
        }

        public void SetProgress(float progress)
        {
            this.Progress = progress;
            Refresh();
        }

        public new void Refresh()
        {
            if (!this.IsDisposed) {
                this.BeginInvoke((MethodInvoker)delegate() {
                    this.Text = string.Format("{0:F0}% {1} ({2})", Progress * 100, System.IO.Path.GetFileName(Filename), Archive);
                    progressBar1.Value = Math.Max(0, Math.Min(100, (int)(Progress * 100)));
                    txtStatus.Text = Status;

                    long total = Compressed + SavedByCompression + SavedByInternalDelta + SavedByExternalDelta;
                    txtTotalProcessed.Text = FormatSize(total);
                    txtCompressed.Text = FormatSize(Compressed, total);
                    txtSavedByCompression.Text = FormatSize(SavedByCompression, total);
                    txtSavedByInternalDelta.Text = FormatSize(SavedByInternalDelta, total);
                    txtSavedByExternalDelta.Text = FormatSize(SavedByExternalDelta, total);
                });
            }
        }

        string FormatSize(long size, long outOf)
        {
            return FormatSize(size) + string.Format(" ({0}%)", outOf == 0 ? 0 : 100 * size / outOf);
        }

        string FormatSize(long size)
        {
            if (size >= (long)10 * 1024 * 1024 * 1024) return (size / (1024 * 1024 * 1024))        .ToString()     + " GiB";
            if (size >= 1024 * 1024 * 1024)            return ((float)size / (1024 * 1024 * 1024)) .ToString("F1") + " GiB";
            if (size >= 10 * 1024 * 1024)              return (size / (1024 * 1024))               .ToString()     + " MiB";
            if (size >= 1024 * 1024)                   return ((float)size / (1024 * 1024))        .ToString("F1") + " MiB";
            if (size >= 10 * 1024)                     return (size / (1024))                      .ToString()     + " KiB";
            if (size >= 1024)                          return ((float)size / (1024))               .ToString("F1") + " KiB";
            return size.ToString() + " B";
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Canceled = true;
            buttonCancel.Enabled = false;
        }

        private void ProgressBar_FormClosing(object sender, FormClosingEventArgs e)
        {
            Canceled = true;
        }

        public void DisableCancelButton()
        {
            if (!this.IsDisposed) {
                this.BeginInvoke((MethodInvoker)delegate() {
                    buttonCancel.Enabled = false;
                });
            }
        }
    }
}
