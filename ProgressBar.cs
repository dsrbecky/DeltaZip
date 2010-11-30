using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace DeltaZip
{
    public partial class ProgressBar : Form
    {
        MethodInvoker onTick;
        MethodInvoker onCancel;

        public ProgressBar()
        {
            InitializeComponent();
            timer1.Tick += delegate { onTick(); };
            buttonCancel.Click += delegate { onCancel(); };
        }

        public void Bind(ArchiveWriter.Stats stats)
        {
            onTick = delegate {
                this.Text = string.Format("{0:F0}% {1}", stats.Progress * 100, stats.Title);
                progressBar1.Value = Math.Max(0, Math.Min(100, (int)(stats.Progress * 100)));
                textStatus.Text = stats.Status;

                long total = stats.Compressed + stats.SavedByCompression + stats.SavedByInternalDelta + stats.SavedByExternalDelta;
                TimeSpan time = (stats.EndTime ?? DateTime.Now) - stats.StartTime;
                labelL1.Text = "Total processed:";          textL1.Text = FormatSize(total);
                labelL2.Text = "Elapsed time:";             textL2.Text = string.Format("{0}:{1:D2}:{2:D2}", time.Hours, time.Minutes, time.Seconds);
                labelL3.Text = "Block size:";               textL3.Text = FormatSize(Settings.SplitterBlockSize);
                labelL4.Text = "";                          textL4.Text = "";
                labelR1.Text = "Compressed size:";          textR1.Text = FormatSize(stats.Compressed, total);
                labelR2.Text = "Saved by compression:";     textR2.Text = FormatSize(stats.SavedByCompression, total);
                labelR3.Text = "Saved by internal delta:";  textR3.Text = FormatSize(stats.SavedByInternalDelta, total);
                labelR4.Text = "Saved by external delta:";  textR4.Text = FormatSize(stats.SavedByExternalDelta, total);

                buttonCancel.Enabled = (stats.EndTime == null);
            };
            onCancel = delegate {
                stats.Canceled = true;
                stats.EndTime  = DateTime.Now;
            };
        }

        public void Bind(ArchiveReader.Stats stats)
        {
            onTick = delegate {
                this.Text = string.Format("{0:F0}% {1}", stats.Progress * 100, stats.Title);
                progressBar1.Value = Math.Max(0, Math.Min(100, (int)(stats.Progress * 100)));
                textStatus.Text = stats.Status;

                long total = stats.Unmodified + stats.ReadFromArchive + stats.ReadFromWorkingCopy;
                TimeSpan time = (stats.EndTime ?? DateTime.Now) - stats.StartTime;
                long writeSpeed = (int)time.TotalSeconds == 0 ? 0 : (stats.ReadFromArchive + stats.ReadFromWorkingCopy) / (int)time.TotalSeconds;
                labelL1.Text = "Total processed:";          textL1.Text = FormatSize(total);
                labelL2.Text = "Elapsed time:";             textL2.Text = string.Format("{0}:{1:D2}:{2:D2}", time.Hours, time.Minutes, time.Seconds);
                labelL3.Text = "Average write speed:";      textL3.Text = FormatSize(writeSpeed) + "/s";
                labelL4.Text = "";                          textL4.Text = "";
                labelR1.Text = "Unmodified:";               textR1.Text = FormatSize(stats.Unmodified, total);
                labelR2.Text = "Read from archive:";        textR2.Text = FormatSize(stats.ReadFromArchive, total);
                labelR3.Text = "Read from working copy:";   textR3.Text = FormatSize(stats.ReadFromWorkingCopy, total);
                labelR4.Text = "";                          textR4.Text = "";

                buttonCancel.Enabled = (stats.EndTime == null);
            };
            onCancel = delegate {
                stats.Canceled = true;
                stats.EndTime  = DateTime.Now;
            };
        }

        static string FormatSize(long size, long outOf)
        {
            return FormatSize(size) + string.Format(" ({0}%)", outOf == 0 ? 0 : 100 * size / outOf);
        }

        static string FormatSize(long size)
        {
            if (size >= (long)10 * 1024 * 1024 * 1024) return (size / (1024 * 1024 * 1024))        .ToString()     + " GiB";
            if (size >= 1024 * 1024 * 1024)            return ((float)size / (1024 * 1024 * 1024)) .ToString("F1") + " GiB";
            if (size >= 10 * 1024 * 1024)              return (size / (1024 * 1024))               .ToString()     + " MiB";
            if (size >= 1024 * 1024)                   return ((float)size / (1024 * 1024))        .ToString("F1") + " MiB";
            if (size >= 10 * 1024)                     return (size / (1024))                      .ToString()     + " KiB";
            if (size >= 1024)                          return ((float)size / (1024))               .ToString("F1") + " KiB";
            return size.ToString() + " B";
        }

        private void ProgressBar_FormClosing(object sender, FormClosingEventArgs e)
        {
            buttonCancel.PerformClick();
        }

        private void ProgressBar_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

        public new void Close()
        {
            this.BeginInvoke((MethodInvoker)delegate { base.Close(); });
        }
    }
}
