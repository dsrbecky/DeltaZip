namespace DeltaZip
{
    partial class MainForm
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
            if (disposing && (components != null))
            {
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.optMulti = new System.Windows.Forms.CheckBox();
            this.createBtn = new System.Windows.Forms.Button();
            this.optVerify = new System.Windows.Forms.CheckBox();
            this.createSrcSelect = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.createDst = new System.Windows.Forms.ComboBox();
            this.createDstSelect = new System.Windows.Forms.Button();
            this.createSrc = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.createRefAuto = new System.Windows.Forms.CheckBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.extractDst = new System.Windows.Forms.ComboBox();
            this.extractBtn = new System.Windows.Forms.Button();
            this.extractSrc = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.extractDstSelect = new System.Windows.Forms.Button();
            this.extractSrcSelect = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.verifyBtn = new System.Windows.Forms.Button();
            this.verifySrc = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.verifySrcSelect = new System.Windows.Forms.Button();
            this.createRef = new System.Windows.Forms.ComboBox();
            this.createRefSelect = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(8, 8);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(542, 238);
            this.tabControl1.TabIndex = 3;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.createRef);
            this.tabPage1.Controls.Add(this.createRefSelect);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.optMulti);
            this.tabPage1.Controls.Add(this.createBtn);
            this.tabPage1.Controls.Add(this.optVerify);
            this.tabPage1.Controls.Add(this.createSrcSelect);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.createDst);
            this.tabPage1.Controls.Add(this.createDstSelect);
            this.tabPage1.Controls.Add(this.createSrc);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.createRefAuto);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(534, 212);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Create archive";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(44, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Source:";
            // 
            // optMulti
            // 
            this.optMulti.AutoSize = true;
            this.optMulti.Location = new System.Drawing.Point(75, 134);
            this.optMulti.Name = "optMulti";
            this.optMulti.Size = new System.Drawing.Size(267, 17);
            this.optMulti.TabIndex = 16;
            this.optMulti.Text = "Create multiple archives - one for each subdirectory";
            this.optMulti.UseVisualStyleBackColor = true;
            this.optMulti.CheckedChanged += new System.EventHandler(this.optMulti_CheckedChanged);
            // 
            // createBtn
            // 
            this.createBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.createBtn.Location = new System.Drawing.Point(441, 5);
            this.createBtn.Name = "createBtn";
            this.createBtn.Size = new System.Drawing.Size(75, 23);
            this.createBtn.TabIndex = 0;
            this.createBtn.Text = "Create";
            this.createBtn.UseVisualStyleBackColor = true;
            this.createBtn.Click += new System.EventHandler(this.createBtn_Click);
            // 
            // optVerify
            // 
            this.optVerify.AutoSize = true;
            this.optVerify.Checked = true;
            this.optVerify.CheckState = System.Windows.Forms.CheckState.Checked;
            this.optVerify.Location = new System.Drawing.Point(75, 111);
            this.optVerify.Name = "optVerify";
            this.optVerify.Size = new System.Drawing.Size(90, 17);
            this.optVerify.TabIndex = 15;
            this.optVerify.Text = "Verify archive";
            this.optVerify.UseVisualStyleBackColor = true;
            // 
            // createSrcSelect
            // 
            this.createSrcSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.createSrcSelect.Location = new System.Drawing.Point(411, 5);
            this.createSrcSelect.Name = "createSrcSelect";
            this.createSrcSelect.Size = new System.Drawing.Size(24, 23);
            this.createSrcSelect.TabIndex = 3;
            this.createSrcSelect.Text = "...";
            this.createSrcSelect.UseVisualStyleBackColor = true;
            this.createSrcSelect.Click += new System.EventHandler(this.createSrcSelect_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(10, 88);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(46, 13);
            this.label6.TabIndex = 14;
            this.label6.Text = "Options:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 36);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Archive:";
            // 
            // createDst
            // 
            this.createDst.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.createDst.FormattingEnabled = true;
            this.createDst.Location = new System.Drawing.Point(75, 32);
            this.createDst.Name = "createDst";
            this.createDst.Size = new System.Drawing.Size(336, 21);
            this.createDst.TabIndex = 13;
            // 
            // createDstSelect
            // 
            this.createDstSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.createDstSelect.Location = new System.Drawing.Point(411, 31);
            this.createDstSelect.Name = "createDstSelect";
            this.createDstSelect.Size = new System.Drawing.Size(24, 23);
            this.createDstSelect.TabIndex = 6;
            this.createDstSelect.Text = "...";
            this.createDstSelect.UseVisualStyleBackColor = true;
            this.createDstSelect.Click += new System.EventHandler(this.createDstSelect_Click);
            // 
            // createSrc
            // 
            this.createSrc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.createSrc.FormattingEnabled = true;
            this.createSrc.Location = new System.Drawing.Point(75, 6);
            this.createSrc.Name = "createSrc";
            this.createSrc.Size = new System.Drawing.Size(336, 21);
            this.createSrc.TabIndex = 12;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 62);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Refernce:";
            // 
            // refsAuto
            // 
            this.createRefAuto.AutoSize = true;
            this.createRefAuto.Checked = true;
            this.createRefAuto.CheckState = System.Windows.Forms.CheckState.Checked;
            this.createRefAuto.Location = new System.Drawing.Point(75, 88);
            this.createRefAuto.Name = "refsAuto";
            this.createRefAuto.Size = new System.Drawing.Size(267, 17);
            this.createRefAuto.TabIndex = 9;
            this.createRefAuto.Text = "Automatically add most recent archive as reference";
            this.createRefAuto.UseVisualStyleBackColor = true;
            this.createRefAuto.CheckedChanged += new System.EventHandler(this.refsAuto_CheckedChanged);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.extractDst);
            this.tabPage2.Controls.Add(this.extractBtn);
            this.tabPage2.Controls.Add(this.extractSrc);
            this.tabPage2.Controls.Add(this.label4);
            this.tabPage2.Controls.Add(this.extractDstSelect);
            this.tabPage2.Controls.Add(this.extractSrcSelect);
            this.tabPage2.Controls.Add(this.label5);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(534, 212);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Extract archive";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // extractDst
            // 
            this.extractDst.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.extractDst.FormattingEnabled = true;
            this.extractDst.Location = new System.Drawing.Point(75, 32);
            this.extractDst.Name = "extractDst";
            this.extractDst.Size = new System.Drawing.Size(336, 21);
            this.extractDst.TabIndex = 14;
            // 
            // extractBtn
            // 
            this.extractBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.extractBtn.Location = new System.Drawing.Point(441, 5);
            this.extractBtn.Name = "extractBtn";
            this.extractBtn.Size = new System.Drawing.Size(75, 23);
            this.extractBtn.TabIndex = 0;
            this.extractBtn.Text = "Extract";
            this.extractBtn.UseVisualStyleBackColor = true;
            this.extractBtn.Click += new System.EventHandler(this.extractBtn_Click);
            // 
            // extractSrc
            // 
            this.extractSrc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.extractSrc.FormattingEnabled = true;
            this.extractSrc.Location = new System.Drawing.Point(75, 6);
            this.extractSrc.Name = "extractSrc";
            this.extractSrc.Size = new System.Drawing.Size(336, 21);
            this.extractSrc.TabIndex = 13;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(10, 10);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(46, 13);
            this.label4.TabIndex = 7;
            this.label4.Text = "Archive:";
            // 
            // extractDstSelect
            // 
            this.extractDstSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.extractDstSelect.Location = new System.Drawing.Point(411, 31);
            this.extractDstSelect.Name = "extractDstSelect";
            this.extractDstSelect.Size = new System.Drawing.Size(24, 23);
            this.extractDstSelect.TabIndex = 12;
            this.extractDstSelect.Text = "...";
            this.extractDstSelect.UseVisualStyleBackColor = true;
            this.extractDstSelect.Click += new System.EventHandler(this.extractDstSelect_Click);
            // 
            // extractSrcSelect
            // 
            this.extractSrcSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.extractSrcSelect.Location = new System.Drawing.Point(411, 5);
            this.extractSrcSelect.Name = "extractSrcSelect";
            this.extractSrcSelect.Size = new System.Drawing.Size(24, 23);
            this.extractSrcSelect.TabIndex = 9;
            this.extractSrcSelect.Text = "...";
            this.extractSrcSelect.UseVisualStyleBackColor = true;
            this.extractSrcSelect.Click += new System.EventHandler(this.extractSrcSelect_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(10, 36);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(63, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Destination:";
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.verifyBtn);
            this.tabPage3.Controls.Add(this.verifySrc);
            this.tabPage3.Controls.Add(this.label7);
            this.tabPage3.Controls.Add(this.verifySrcSelect);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(534, 212);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Verify archive";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // verifyBtn
            // 
            this.verifyBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.verifyBtn.Location = new System.Drawing.Point(441, 5);
            this.verifyBtn.Name = "verifyBtn";
            this.verifyBtn.Size = new System.Drawing.Size(75, 23);
            this.verifyBtn.TabIndex = 14;
            this.verifyBtn.Text = "Verify";
            this.verifyBtn.UseVisualStyleBackColor = true;
            this.verifyBtn.Click += new System.EventHandler(this.verifyBtn_Click);
            // 
            // verifySrc
            // 
            this.verifySrc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.verifySrc.FormattingEnabled = true;
            this.verifySrc.Location = new System.Drawing.Point(75, 6);
            this.verifySrc.Name = "verifySrc";
            this.verifySrc.Size = new System.Drawing.Size(336, 21);
            this.verifySrc.TabIndex = 17;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(10, 10);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(46, 13);
            this.label7.TabIndex = 15;
            this.label7.Text = "Archive:";
            // 
            // verifySrcSelect
            // 
            this.verifySrcSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.verifySrcSelect.Location = new System.Drawing.Point(411, 5);
            this.verifySrcSelect.Name = "verifySrcSelect";
            this.verifySrcSelect.Size = new System.Drawing.Size(24, 23);
            this.verifySrcSelect.TabIndex = 16;
            this.verifySrcSelect.Text = "...";
            this.verifySrcSelect.UseVisualStyleBackColor = true;
            this.verifySrcSelect.Click += new System.EventHandler(this.verifySrcSelect_Click);
            // 
            // createRef
            // 
            this.createRef.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.createRef.FormattingEnabled = true;
            this.createRef.Location = new System.Drawing.Point(75, 58);
            this.createRef.Name = "createRef";
            this.createRef.Size = new System.Drawing.Size(336, 21);
            this.createRef.TabIndex = 18;
            // 
            // createRefSelect
            // 
            this.createRefSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.createRefSelect.Location = new System.Drawing.Point(411, 57);
            this.createRefSelect.Name = "createRefSelect";
            this.createRefSelect.Size = new System.Drawing.Size(24, 23);
            this.createRefSelect.TabIndex = 17;
            this.createRefSelect.Text = "...";
            this.createRefSelect.UseVisualStyleBackColor = true;
            this.createRefSelect.Click += new System.EventHandler(this.createRefSelect_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(557, 254);
            this.Controls.Add(this.tabControl1);
            this.Name = "MainForm";
            this.Text = "Delta Zip";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox optMulti;
        private System.Windows.Forms.Button createBtn;
        private System.Windows.Forms.CheckBox optVerify;
        private System.Windows.Forms.Button createSrcSelect;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox createDst;
        private System.Windows.Forms.Button createDstSelect;
        private System.Windows.Forms.ComboBox createSrc;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox createRefAuto;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.ComboBox extractDst;
        private System.Windows.Forms.Button extractBtn;
        private System.Windows.Forms.ComboBox extractSrc;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button extractDstSelect;
        private System.Windows.Forms.Button extractSrcSelect;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Button verifyBtn;
        private System.Windows.Forms.ComboBox verifySrc;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button verifySrcSelect;
        private System.Windows.Forms.ComboBox createRef;
        private System.Windows.Forms.Button createRefSelect;


    }
}

