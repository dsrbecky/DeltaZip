namespace DeltaZip
{
    partial class ProgressBar
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.txtStatus = new System.Windows.Forms.Label();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.txtCompressed = new System.Windows.Forms.Label();
            this.txtSavedByCompression = new System.Windows.Forms.Label();
            this.txtSavedByInternalDelta = new System.Windows.Forms.Label();
            this.txtSavedByExternalDelta = new System.Windows.Forms.Label();
            this.txtTotalProcessed = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(12, 155);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(476, 23);
            this.progressBar1.TabIndex = 0;
            // 
            // txtStatus
            // 
            this.txtStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtStatus.AutoEllipsis = true;
            this.txtStatus.Location = new System.Drawing.Point(12, 134);
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.Size = new System.Drawing.Size(476, 18);
            this.txtStatus.TabIndex = 1;
            this.txtStatus.Text = "-";
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(413, 184);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 34);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Compressed size:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(11, 54);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Saved by compression:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(11, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(118, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Saved by internal delta:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(11, 94);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(121, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Saved by external delta:";
            // 
            // txtCompressed
            // 
            this.txtCompressed.AutoSize = true;
            this.txtCompressed.Location = new System.Drawing.Point(156, 34);
            this.txtCompressed.Name = "txtCompressed";
            this.txtCompressed.Size = new System.Drawing.Size(10, 13);
            this.txtCompressed.TabIndex = 7;
            this.txtCompressed.Text = "-";
            // 
            // txtSavedByCompression
            // 
            this.txtSavedByCompression.AutoSize = true;
            this.txtSavedByCompression.Location = new System.Drawing.Point(156, 54);
            this.txtSavedByCompression.Name = "txtSavedByCompression";
            this.txtSavedByCompression.Size = new System.Drawing.Size(10, 13);
            this.txtSavedByCompression.TabIndex = 8;
            this.txtSavedByCompression.Text = "-";
            // 
            // txtSavedByInternalDelta
            // 
            this.txtSavedByInternalDelta.AutoSize = true;
            this.txtSavedByInternalDelta.Location = new System.Drawing.Point(156, 74);
            this.txtSavedByInternalDelta.Name = "txtSavedByInternalDelta";
            this.txtSavedByInternalDelta.Size = new System.Drawing.Size(10, 13);
            this.txtSavedByInternalDelta.TabIndex = 9;
            this.txtSavedByInternalDelta.Text = "-";
            // 
            // txtSavedByExternalDelta
            // 
            this.txtSavedByExternalDelta.AutoSize = true;
            this.txtSavedByExternalDelta.Location = new System.Drawing.Point(156, 94);
            this.txtSavedByExternalDelta.Name = "txtSavedByExternalDelta";
            this.txtSavedByExternalDelta.Size = new System.Drawing.Size(10, 13);
            this.txtSavedByExternalDelta.TabIndex = 10;
            this.txtSavedByExternalDelta.Text = "-";
            // 
            // txtTotalProcessed
            // 
            this.txtTotalProcessed.AutoSize = true;
            this.txtTotalProcessed.Location = new System.Drawing.Point(156, 14);
            this.txtTotalProcessed.Name = "txtTotalProcessed";
            this.txtTotalProcessed.Size = new System.Drawing.Size(10, 13);
            this.txtTotalProcessed.TabIndex = 12;
            this.txtTotalProcessed.Text = "-";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(11, 14);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(86, 13);
            this.label6.TabIndex = 11;
            this.label6.Text = "Total processed:";
            // 
            // ProgressBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(500, 219);
            this.Controls.Add(this.txtTotalProcessed);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.txtSavedByExternalDelta);
            this.Controls.Add(this.txtSavedByInternalDelta);
            this.Controls.Add(this.txtSavedByCompression);
            this.Controls.Add(this.txtCompressed);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.txtStatus);
            this.Controls.Add(this.progressBar1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "ProgressBar";
            this.Text = "0%";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ProgressBar_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label txtStatus;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label txtCompressed;
        private System.Windows.Forms.Label txtSavedByCompression;
        private System.Windows.Forms.Label txtSavedByInternalDelta;
        private System.Windows.Forms.Label txtSavedByExternalDelta;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Label txtTotalProcessed;
        private System.Windows.Forms.Label label6;
    }
}