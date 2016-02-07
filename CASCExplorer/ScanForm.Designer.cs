namespace CASCExplorer
{
    partial class ScanForm
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
            this.scanProgressBar = new System.Windows.Forms.ProgressBar();
            this.scanButton = new System.Windows.Forms.Button();
            this.scanLabel = new System.Windows.Forms.Label();
            this.scanBackgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.progressLabel = new System.Windows.Forms.Label();
            this.filenameTextBox = new System.Windows.Forms.TextBox();
            this.missingFilesLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // scanProgressBar
            // 
            this.scanProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.scanProgressBar.Location = new System.Drawing.Point(12, 25);
            this.scanProgressBar.Name = "scanProgressBar";
            this.scanProgressBar.Size = new System.Drawing.Size(731, 23);
            this.scanProgressBar.TabIndex = 0;
            // 
            // scanButton
            // 
            this.scanButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.scanButton.Location = new System.Drawing.Point(749, 25);
            this.scanButton.Name = "scanButton";
            this.scanButton.Size = new System.Drawing.Size(73, 23);
            this.scanButton.TabIndex = 1;
            this.scanButton.Text = "Start";
            this.scanButton.UseVisualStyleBackColor = true;
            this.scanButton.Click += new System.EventHandler(this.scanButton_Click);
            // 
            // scanLabel
            // 
            this.scanLabel.AutoSize = true;
            this.scanLabel.Location = new System.Drawing.Point(9, 9);
            this.scanLabel.Name = "scanLabel";
            this.scanLabel.Size = new System.Drawing.Size(38, 13);
            this.scanLabel.TabIndex = 3;
            this.scanLabel.Text = "Ready";
            // 
            // scanBackgroundWorker
            // 
            this.scanBackgroundWorker.WorkerReportsProgress = true;
            this.scanBackgroundWorker.WorkerSupportsCancellation = true;
            this.scanBackgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.scanBackgroundWorker_DoWork);
            this.scanBackgroundWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.scanBackgroundWorker_ProgressChanged);
            this.scanBackgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.scanBackgroundWorker_RunWorkerCompleted);
            // 
            // progressLabel
            // 
            this.progressLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.progressLabel.Location = new System.Drawing.Point(625, 9);
            this.progressLabel.Name = "progressLabel";
            this.progressLabel.Size = new System.Drawing.Size(121, 13);
            this.progressLabel.TabIndex = 4;
            this.progressLabel.Text = "0/0";
            this.progressLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // filenameTextBox
            // 
            this.filenameTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.filenameTextBox.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.filenameTextBox.Location = new System.Drawing.Point(12, 78);
            this.filenameTextBox.MaxLength = 33554432;
            this.filenameTextBox.Multiline = true;
            this.filenameTextBox.Name = "filenameTextBox";
            this.filenameTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.filenameTextBox.Size = new System.Drawing.Size(810, 289);
            this.filenameTextBox.TabIndex = 5;
            // 
            // missingFilesLabel
            // 
            this.missingFilesLabel.AutoSize = true;
            this.missingFilesLabel.Location = new System.Drawing.Point(9, 62);
            this.missingFilesLabel.Name = "missingFilesLabel";
            this.missingFilesLabel.Size = new System.Drawing.Size(125, 13);
            this.missingFilesLabel.TabIndex = 6;
            this.missingFilesLabel.Text = "Missing file names found:";
            // 
            // ScanForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(834, 379);
            this.Controls.Add(this.missingFilesLabel);
            this.Controls.Add(this.filenameTextBox);
            this.Controls.Add(this.progressLabel);
            this.Controls.Add(this.scanLabel);
            this.Controls.Add(this.scanButton);
            this.Controls.Add(this.scanProgressBar);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(350, 250);
            this.Name = "ScanForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Scan all files for missing file names";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar scanProgressBar;
        private System.Windows.Forms.Button scanButton;
        private System.Windows.Forms.Label scanLabel;
        private System.ComponentModel.BackgroundWorker scanBackgroundWorker;
        private System.Windows.Forms.Label progressLabel;
        private System.Windows.Forms.TextBox filenameTextBox;
        private System.Windows.Forms.Label missingFilesLabel;
    }
}