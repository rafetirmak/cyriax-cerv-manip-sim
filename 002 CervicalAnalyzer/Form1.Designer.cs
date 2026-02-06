namespace CervicalAnalyzer
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        
        // Controls
        private System.Windows.Forms.PictureBox canvas;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.Button btnAnalyze;
        private System.Windows.Forms.GroupBox grpMarkers;
        
        // --- 4 ADET İŞARETLEYİCİ SEÇENEĞİ ---
        private System.Windows.Forms.RadioButton rbTraStart;   // 1. Traction Start
        private System.Windows.Forms.RadioButton rbRotStart;   // 2. Rotation Start
        private System.Windows.Forms.RadioButton rbRotPeak;    // 3. Rotation Peak
        private System.Windows.Forms.RadioButton rbManip;      // 4. Manipulation

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.canvas = new System.Windows.Forms.PictureBox();
            this.panelTop = new System.Windows.Forms.Panel();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnAnalyze = new System.Windows.Forms.Button();
            this.grpMarkers = new System.Windows.Forms.GroupBox();
            this.rbTraStart = new System.Windows.Forms.RadioButton();
            this.rbRotStart = new System.Windows.Forms.RadioButton();
            this.rbRotPeak = new System.Windows.Forms.RadioButton();
            this.rbManip = new System.Windows.Forms.RadioButton();
            
            ((System.ComponentModel.ISupportInitialize)(this.canvas)).BeginInit();
            this.panelTop.SuspendLayout();
            this.grpMarkers.SuspendLayout();
            this.SuspendLayout();

            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.btnAnalyze);
            this.panelTop.Controls.Add(this.grpMarkers);
            this.panelTop.Controls.Add(this.btnLoad);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Height = 110; 
            this.panelTop.BackColor = System.Drawing.Color.WhiteSmoke;
            this.panelTop.Padding = new System.Windows.Forms.Padding(10);
            
            // 
            // btnLoad
            // 
            this.btnLoad.Location = new System.Drawing.Point(20, 30);
            this.btnLoad.Size = new System.Drawing.Size(130, 50);
            this.btnLoad.Text = "📂 Load JSON";
            this.btnLoad.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnLoad.BackColor = System.Drawing.Color.LightBlue;
            this.btnLoad.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);

            // 
            // grpMarkers (Selection Group)
            // 
            this.grpMarkers.Controls.Add(this.rbManip);
            this.grpMarkers.Controls.Add(this.rbRotPeak);
            this.grpMarkers.Controls.Add(this.rbRotStart);
            this.grpMarkers.Controls.Add(this.rbTraStart);
            this.grpMarkers.Location = new System.Drawing.Point(170, 10);
            this.grpMarkers.Size = new System.Drawing.Size(260, 90); // Genişletildi
            this.grpMarkers.Text = "Marker Selection";
            this.grpMarkers.Font = new System.Drawing.Font("Segoe UI", 9F);

            // 
            // rbTraStart
            // 
            this.rbTraStart.Location = new System.Drawing.Point(15, 20);
            this.rbTraStart.Text = "1. Traction Start";
            this.rbTraStart.AutoSize = true;
            this.rbTraStart.Checked = true;

            // 
            // rbRotStart
            // 
            this.rbRotStart.Location = new System.Drawing.Point(15, 40);
            this.rbRotStart.Text = "2. Rotation Start";
            this.rbRotStart.AutoSize = true;

            // 
            // rbRotPeak
            // 
            this.rbRotPeak.Location = new System.Drawing.Point(135, 20); // Sağ Sütun
            this.rbRotPeak.Text = "3. Rotation Peak";
            this.rbRotPeak.AutoSize = true;

            // 
            // rbManip
            // 
            this.rbManip.Location = new System.Drawing.Point(135, 40); // Sağ Sütun
            this.rbManip.Text = "4. Manipulation";
            this.rbManip.AutoSize = true;

            // 
            // btnAnalyze
            // 
            this.btnAnalyze.Location = new System.Drawing.Point(450, 30);
            this.btnAnalyze.Size = new System.Drawing.Size(130, 50);
            this.btnAnalyze.Text = "📊 REPORT";
            this.btnAnalyze.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnAnalyze.BackColor = System.Drawing.Color.LightGreen;
            this.btnAnalyze.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAnalyze.Click += new System.EventHandler(this.btnAnalyze_Click);

            // 
            // canvas
            // 
            this.canvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvas.BackColor = System.Drawing.Color.White;

            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(950, 600);
            this.Controls.Add(this.canvas);
            this.Controls.Add(this.panelTop);
            this.Text = "Cervical Analysis Tool - Cyriax Edition";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            
            ((System.ComponentModel.ISupportInitialize)(this.canvas)).EndInit();
            this.panelTop.ResumeLayout(false);
            this.grpMarkers.ResumeLayout(false);
            this.grpMarkers.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}