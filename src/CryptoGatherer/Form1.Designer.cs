using System;
using System.Linq;

namespace CryptoGatherer
{
    partial class Form1
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
            this.fileContents = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.button2 = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.fileName = new System.Windows.Forms.TextBox();
            this.isFullFile = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.language = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.sourceUrl = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.packageName = new System.Windows.Forms.TextBox();
            this.algorithms = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.fileListView = new System.Windows.Forms.ListView();
            this.colFilename = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colPackage = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colAlgorithms = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.menuStrip1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // fileContents
            // 
            this.fileContents.Font = new System.Drawing.Font("Consolas", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.fileContents.Location = new System.Drawing.Point(996, 427);
            this.fileContents.Multiline = true;
            this.fileContents.Name = "fileContents";
            this.fileContents.Size = new System.Drawing.Size(787, 451);
            this.fileContents.TabIndex = 6;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(350, 288);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(241, 39);
            this.button1.TabIndex = 7;
            this.button1.Text = "Save";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(2175, 28);
            this.menuStrip1.TabIndex = 13;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(47, 26);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button2);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.fileName);
            this.groupBox1.Controls.Add(this.isFullFile);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.language);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.sourceUrl);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.packageName);
            this.groupBox1.Controls.Add(this.algorithms);
            this.groupBox1.Location = new System.Drawing.Point(996, 56);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(597, 353);
            this.groupBox1.TabIndex = 14;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Metadata";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(350, 48);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(241, 39);
            this.button2.TabIndex = 10;
            this.button2.Text = "Clear";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 48);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(73, 17);
            this.label6.TabIndex = 9;
            this.label6.Text = "Filename: ";
            // 
            // fileName
            // 
            this.fileName.Location = new System.Drawing.Point(114, 48);
            this.fileName.Name = "fileName";
            this.fileName.Size = new System.Drawing.Size(228, 22);
            this.fileName.TabIndex = 0;
            // 
            // isFullFile
            // 
            this.isFullFile.AutoSize = true;
            this.isFullFile.Location = new System.Drawing.Point(368, 143);
            this.isFullFile.Name = "isFullFile";
            this.isFullFile.Size = new System.Drawing.Size(78, 21);
            this.isFullFile.TabIndex = 5;
            this.isFullFile.Text = "Full File";
            this.isFullFile.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 163);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(74, 17);
            this.label5.TabIndex = 7;
            this.label5.Text = "Algorithms";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 133);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(80, 17);
            this.label4.TabIndex = 6;
            this.label4.Text = "Language: ";
            // 
            // language
            //
            var languages = ((CodeLanguage[])Enum.GetValues(typeof(CodeLanguage))).ToList().Select(x => x.ToString()).ToArray();
            this.language.FormattingEnabled = true;
            this.language.Items.AddRange(languages);
            this.language.Location = new System.Drawing.Point(114, 133);
            this.language.Name = "language";
            this.language.Size = new System.Drawing.Size(228, 24);
            this.language.Sorted = true;
            this.language.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 104);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(93, 17);
            this.label3.TabIndex = 4;
            this.label3.Text = "Source URL: ";
            this.label3.Click += new System.EventHandler(this.label3_Click);
            // 
            // sourceUrl
            // 
            this.sourceUrl.Location = new System.Drawing.Point(114, 105);
            this.sourceUrl.Name = "sourceUrl";
            this.sourceUrl.Size = new System.Drawing.Size(228, 22);
            this.sourceUrl.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 76);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(71, 17);
            this.label2.TabIndex = 2;
            this.label2.Text = "Package: ";
            // 
            // packageName
            // 
            this.packageName.Location = new System.Drawing.Point(114, 76);
            this.packageName.Multiline = true;
            this.packageName.Name = "packageName";
            this.packageName.Size = new System.Drawing.Size(228, 22);
            this.packageName.TabIndex = 1;
            // 
            // algorithms
            // 
            var algorithms = ((CryptoAlgorithm[])Enum.GetValues(typeof(CryptoAlgorithm))).ToList().Select(x => x.ToString()).ToArray();
            this.algorithms.FormattingEnabled = true;
            this.algorithms.ItemHeight = 16;
            this.algorithms.Items.AddRange(algorithms);
            this.algorithms.Location = new System.Drawing.Point(114, 163);
            this.algorithms.Name = "algorithms";
            this.algorithms.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.algorithms.Size = new System.Drawing.Size(228, 164);
            this.algorithms.Sorted = true;
            this.algorithms.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(24, 47);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(71, 17);
            this.label1.TabIndex = 15;
            this.label1.Text = "Data Files";
            // 
            // fileListView
            // 
            this.fileListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colFilename,
            this.colPackage,
            this.colAlgorithms});
            this.fileListView.FullRowSelect = true;
            this.fileListView.HideSelection = false;
            this.fileListView.Location = new System.Drawing.Point(27, 67);
            this.fileListView.MultiSelect = false;
            this.fileListView.Name = "fileListView";
            this.fileListView.Size = new System.Drawing.Size(943, 802);
            this.fileListView.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.fileListView.TabIndex = 16;
            this.fileListView.UseCompatibleStateImageBehavior = false;
            this.fileListView.View = System.Windows.Forms.View.Details;
            this.fileListView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.fileListView_ColumnClick);
            this.fileListView.SelectedIndexChanged += new System.EventHandler(this.fileListBox_SelectedIndexChanged);
            // 
            // colFilename
            // 
            this.colFilename.Text = "Filename";
            this.colFilename.Width = 200;
            // 
            // colPackage
            // 
            this.colPackage.Text = "Language";
            this.colPackage.Width = 153;
            // 
            // colAlgorithms
            // 
            this.colAlgorithms.Text = "Algorithms";
            this.colAlgorithms.Width = 140;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2175, 899);
            this.Controls.Add(this.fileListView);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.fileContents);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "CryptoGatherer";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox fileContents;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox language;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox sourceUrl;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox packageName;
        private System.Windows.Forms.ListBox algorithms;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox fileName;
        private System.Windows.Forms.CheckBox isFullFile;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.ListView fileListView;
        private System.Windows.Forms.ColumnHeader colFilename;
        private System.Windows.Forms.ColumnHeader colPackage;
        private System.Windows.Forms.ColumnHeader colAlgorithms;
    }
}

