namespace CervicalForceSim
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        // Ana Taşıyıcılar
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.PictureBox canvas;
        
        // Sol Panel Kontrolleri
        private System.Windows.Forms.GroupBox grpAcquisition;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnSaveCsv; // Yeni
        private System.Windows.Forms.Label lblStatus;

        private System.Windows.Forms.GroupBox grpValues;
        private System.Windows.Forms.Label lblCurForce;
        private System.Windows.Forms.Label lblCurAngle;
        private System.Windows.Forms.TextBox txtForceVal;
        private System.Windows.Forms.TextBox txtAngleVal;

        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.CheckBox chkFilter; // Yeni Filtre Seçeneği
        private System.Windows.Forms.Label lblTargetForce;
        private System.Windows.Forms.NumericUpDown numTargetForce; // Hedef Kuvvet Ayarı

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.grpAcquisition = new System.Windows.Forms.GroupBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnSaveCsv = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.grpValues = new System.Windows.Forms.GroupBox();
            this.txtAngleVal = new System.Windows.Forms.TextBox();
            this.txtForceVal = new System.Windows.Forms.TextBox();
            this.lblCurAngle = new System.Windows.Forms.Label();
            this.lblCurForce = new System.Windows.Forms.Label();
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.numTargetForce = new System.Windows.Forms.NumericUpDown();
            this.lblTargetForce = new System.Windows.Forms.Label();
            this.chkFilter = new System.Windows.Forms.CheckBox();
            this.canvas = new System.Windows.Forms.PictureBox();

            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.grpAcquisition.SuspendLayout();
            this.grpValues.SuspendLayout();
            this.grpSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numTargetForce)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.canvas)).BeginInit();
            this.SuspendLayout();

            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1 (Sol Panel - Kontroller)
            // 
            this.splitContainer1.Panel1.BackColor = System.Drawing.SystemColors.Control;
            this.splitContainer1.Panel1.Controls.Add(this.grpSettings);
            this.splitContainer1.Panel1.Controls.Add(this.grpValues);
            this.splitContainer1.Panel1.Controls.Add(this.grpAcquisition);
            this.splitContainer1.Panel1.Padding = new System.Windows.Forms.Padding(10);
            // 
            // splitContainer1.Panel2 (Sağ Panel - Grafik)
            // 
            this.splitContainer1.Panel2.Controls.Add(this.canvas);
            this.splitContainer1.Size = new System.Drawing.Size(1000, 600);
            this.splitContainer1.SplitterDistance = 220; // Sol panel genişliği
            this.splitContainer1.TabIndex = 0;

            // 
            // grpAcquisition
            // 
            this.grpAcquisition.Controls.Add(this.lblStatus);
            this.grpAcquisition.Controls.Add(this.btnSaveCsv);
            this.grpAcquisition.Controls.Add(this.btnStop);
            this.grpAcquisition.Controls.Add(this.btnStart);
            this.grpAcquisition.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpAcquisition.Location = new System.Drawing.Point(10, 10);
            this.grpAcquisition.Name = "grpAcquisition";
            this.grpAcquisition.Size = new System.Drawing.Size(200, 180);
            this.grpAcquisition.TabIndex = 0;
            this.grpAcquisition.TabStop = false;
            this.grpAcquisition.Text = "Acquisition Control";

            // btnStart
            this.btnStart.Location = new System.Drawing.Point(15, 30);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(170, 35);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "START";
            this.btnStart.BackColor = System.Drawing.Color.LightGreen;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);

            // btnStop
            this.btnStop.Location = new System.Drawing.Point(15, 71);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(170, 35);
            this.btnStop.TabIndex = 1;
            this.btnStop.Text = "STOP";
            this.btnStop.BackColor = System.Drawing.Color.LightCoral;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);

            // btnSaveCsv
            this.btnSaveCsv.Location = new System.Drawing.Point(15, 112);
            this.btnSaveCsv.Name = "btnSaveCsv";
            this.btnSaveCsv.Size = new System.Drawing.Size(170, 35);
            this.btnSaveCsv.TabIndex = 2;
            this.btnSaveCsv.Text = "SAVE DATA (.CSV)";
            this.btnSaveCsv.Click += new System.EventHandler(this.btnSaveCsv_Click);

            // lblStatus
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(15, 155);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Text = "Ready.";

            // 
            // grpValues
            // 
            this.grpValues.Controls.Add(this.txtAngleVal);
            this.grpValues.Controls.Add(this.txtForceVal);
            this.grpValues.Controls.Add(this.lblCurAngle);
            this.grpValues.Controls.Add(this.lblCurForce);
            this.grpValues.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpValues.Location = new System.Drawing.Point(10, 190);
            this.grpValues.Name = "grpValues";
            this.grpValues.Size = new System.Drawing.Size(200, 100);
            this.grpValues.TabIndex = 1;
            this.grpValues.TabStop = false;
            this.grpValues.Text = "Live Values";

            // Labels and Textboxes
            this.lblCurForce.Location = new System.Drawing.Point(10, 30);
            this.lblCurForce.Text = "Force (kgf):";
            this.lblCurForce.AutoSize = true;

            this.txtForceVal.Location = new System.Drawing.Point(90, 27);
            this.txtForceVal.Size = new System.Drawing.Size(95, 27);
            this.txtForceVal.ReadOnly = true;
            this.txtForceVal.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.lblCurAngle.Location = new System.Drawing.Point(10, 65);
            this.lblCurAngle.Text = "Angle (°):";
            this.lblCurAngle.AutoSize = true;

            this.txtAngleVal.Location = new System.Drawing.Point(90, 62);
            this.txtAngleVal.Size = new System.Drawing.Size(95, 27);
            this.txtAngleVal.ReadOnly = true;
            this.txtAngleVal.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // 
            // grpSettings
            // 
            this.grpSettings.Controls.Add(this.numTargetForce);
            this.grpSettings.Controls.Add(this.lblTargetForce);
            this.grpSettings.Controls.Add(this.chkFilter);
            this.grpSettings.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpSettings.Location = new System.Drawing.Point(10, 290);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(200, 120);
            this.grpSettings.TabIndex = 2;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Settings";

            // chkFilter
            this.chkFilter.AutoSize = true;
            this.chkFilter.Location = new System.Drawing.Point(15, 30);
            this.chkFilter.Text = "Apply Low-Pass Filter";
            this.chkFilter.Checked = true;

            // Target Force
            this.lblTargetForce.AutoSize = true;
            this.lblTargetForce.Location = new System.Drawing.Point(15, 60);
            this.lblTargetForce.Text = "Target Force (kgf):";

            this.numTargetForce.Location = new System.Drawing.Point(15, 85);
            this.numTargetForce.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            this.numTargetForce.Value = new decimal(new int[] { 22, 0, 0, 0 }); // Default 22kg for Cyriax

            // 
            // canvas
            // 
            this.canvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvas.BackColor = System.Drawing.Color.White; // Paper Style: White Background
            this.canvas.Location = new System.Drawing.Point(0, 0);
            this.canvas.Name = "canvas";
            this.canvas.Size = new System.Drawing.Size(776, 600);
            this.canvas.TabIndex = 0;
            this.canvas.TabStop = false;
            this.canvas.Paint += new System.Windows.Forms.PaintEventHandler(this.canvas_Paint);

            // Form1
            this.ClientSize = new System.Drawing.Size(1000, 600);
            this.Controls.Add(this.splitContainer1);
            this.Text = "Cervical Manipulation Simulator (Cyriax Paradigm)";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.grpAcquisition.ResumeLayout(false);
            this.grpAcquisition.PerformLayout();
            this.grpValues.ResumeLayout(false);
            this.grpValues.PerformLayout();
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numTargetForce)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.canvas)).EndInit();
            this.ResumeLayout(false);
        }
    }
}