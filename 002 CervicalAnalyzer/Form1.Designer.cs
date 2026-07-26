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
        private System.Windows.Forms.Button btnExportHtml;
        private System.Windows.Forms.GroupBox grpMarkers;
        private System.Windows.Forms.GroupBox grpForceUnit;
        private System.Windows.Forms.RadioButton rbUnitN;
        private System.Windows.Forms.RadioButton rbUnitKgf;
        
        // --- 4 ADET İŞARETLEYİCİ SEÇENEĞİ ---
        private System.Windows.Forms.RadioButton rbTraStart;   // 1. Traction Start
        private System.Windows.Forms.RadioButton rbRotStart;   // 2. Rotation Start
        private System.Windows.Forms.RadioButton rbRotPeak;    // 3. Rotation Peak
        private System.Windows.Forms.RadioButton rbManip;      // 4. Manipulation
        private System.Windows.Forms.RadioButton rbSetZeroForce;
        private System.Windows.Forms.RadioButton rbSetZeroAngle;
        private System.Windows.Forms.TableLayoutPanel tlpMarkers;
        private System.Windows.Forms.TableLayoutPanel tlpForceUnit;

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
            this.btnExportHtml = new System.Windows.Forms.Button();
            this.grpMarkers = new System.Windows.Forms.GroupBox();
            this.rbTraStart = new System.Windows.Forms.RadioButton();
            this.rbRotStart = new System.Windows.Forms.RadioButton();
            this.rbRotPeak = new System.Windows.Forms.RadioButton();
            this.rbManip = new System.Windows.Forms.RadioButton();
            this.rbSetZeroForce = new System.Windows.Forms.RadioButton();
            this.rbSetZeroAngle = new System.Windows.Forms.RadioButton();
            this.tlpMarkers = new System.Windows.Forms.TableLayoutPanel();
            this.tlpForceUnit = new System.Windows.Forms.TableLayoutPanel();
            this.grpForceUnit = new System.Windows.Forms.GroupBox();
            this.rbUnitN = new System.Windows.Forms.RadioButton();
            this.rbUnitKgf = new System.Windows.Forms.RadioButton();
            
            ((System.ComponentModel.ISupportInitialize)(this.canvas)).BeginInit();
            this.panelTop.SuspendLayout();
            this.grpMarkers.SuspendLayout();
            this.grpForceUnit.SuspendLayout();
            this.tlpMarkers.SuspendLayout();
            this.tlpForceUnit.SuspendLayout();
            this.SuspendLayout();

            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.btnExportHtml);
            this.panelTop.Controls.Add(this.grpForceUnit);
            this.panelTop.Controls.Add(this.btnAnalyze);
            this.panelTop.Controls.Add(this.grpMarkers);
            this.panelTop.Controls.Add(this.btnLoad);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Height = 170; 
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
            // btnExportHtml
            // 
            this.btnExportHtml.Location = new System.Drawing.Point(470, 75);
            this.btnExportHtml.Size = new System.Drawing.Size(130, 45);
            this.btnExportHtml.Text = "🌐 HTML Report";
            this.btnExportHtml.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnExportHtml.BackColor = System.Drawing.Color.LightSeaGreen;
            this.btnExportHtml.ForeColor = System.Drawing.Color.White;
            this.btnExportHtml.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnExportHtml.Click += new System.EventHandler(this.btnExportHtml_Click);

            // 
            // grpMarkers (Selection Group)
            // 
            this.grpMarkers.Controls.Add(this.tlpMarkers);
            this.grpMarkers.Location = new System.Drawing.Point(170, 10);
            this.grpMarkers.AutoSize = true;
            this.grpMarkers.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.grpMarkers.Text = "Marker Selection";
            this.grpMarkers.Font = new System.Drawing.Font("Segoe UI", 9F);
            
            // 
            // tlpMarkers
            // 
            this.tlpMarkers.ColumnCount = 2;
            this.tlpMarkers.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpMarkers.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpMarkers.Controls.Add(this.rbTraStart, 0, 0);
            this.tlpMarkers.Controls.Add(this.rbRotPeak, 1, 0);
            this.tlpMarkers.Controls.Add(this.rbRotStart, 0, 1);
            this.tlpMarkers.Controls.Add(this.rbManip, 1, 1);
            this.tlpMarkers.Controls.Add(this.rbSetZeroForce, 0, 2);
            this.tlpMarkers.Controls.Add(this.rbSetZeroAngle, 1, 2);
            this.tlpMarkers.AutoSize = true;
            this.tlpMarkers.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpMarkers.Location = new System.Drawing.Point(5, 20);
            this.tlpMarkers.RowCount = 3;

            // 
            // rbTraStart
            // 
            this.rbTraStart.Text = "1. Traction Start";
            this.rbTraStart.AutoSize = true;
            this.rbTraStart.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbTraStart.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);
            this.rbTraStart.Checked = true;

            // 
            // rbRotStart
            // 
            this.rbRotStart.Text = "2. Rotation Start";
            this.rbRotStart.AutoSize = true;
            this.rbRotStart.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbRotStart.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);

            // 
            // rbRotPeak
            // 
            this.rbRotPeak.Text = "3. Rotation Peak";
            this.rbRotPeak.AutoSize = true;
            this.rbRotPeak.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbRotPeak.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);

            // 
            // rbManip
            // 
            this.rbManip.Text = "4. Manipulation";
            this.rbManip.AutoSize = true;
            this.rbManip.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbManip.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);

            // 
            // rbSetZeroForce
            // 
            this.rbSetZeroForce.Text = "🔧 Zero Force";
            this.rbSetZeroForce.AutoSize = true;
            this.rbSetZeroForce.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbSetZeroForce.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);

            // 
            // rbSetZeroAngle
            // 
            this.rbSetZeroAngle.Text = "📐 Zero Angle";
            this.rbSetZeroAngle.AutoSize = true;
            this.rbSetZeroAngle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbSetZeroAngle.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);

            // 
            // btnAnalyze
            // 
            this.btnAnalyze.Location = new System.Drawing.Point(470, 15);
            this.btnAnalyze.Size = new System.Drawing.Size(130, 45);
            this.btnAnalyze.Text = "📊 JSON Report";
            this.btnAnalyze.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnAnalyze.BackColor = System.Drawing.Color.LightGreen;
            this.btnAnalyze.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAnalyze.Click += new System.EventHandler(this.btnAnalyze_Click);

            // 
            // grpForceUnit
            // 
            this.grpForceUnit.Controls.Add(this.tlpForceUnit);
            this.grpForceUnit.Location = new System.Drawing.Point(630, 10);
            this.grpForceUnit.AutoSize = true;
            this.grpForceUnit.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.grpForceUnit.Text = "Force Unit";
            this.grpForceUnit.Font = new System.Drawing.Font("Segoe UI", 9F);
            
            // 
            // tlpForceUnit
            // 
            this.tlpForceUnit.ColumnCount = 1;
            this.tlpForceUnit.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpForceUnit.Controls.Add(this.rbUnitN, 0, 0);
            this.tlpForceUnit.Controls.Add(this.rbUnitKgf, 0, 1);
            this.tlpForceUnit.AutoSize = true;
            this.tlpForceUnit.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpForceUnit.Location = new System.Drawing.Point(5, 20);
            this.tlpForceUnit.RowCount = 2;
            
            // 
            // rbUnitN
            // 
            this.rbUnitN.Text = "N (SI)";
            this.rbUnitN.AutoSize = true;
            this.rbUnitN.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbUnitN.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);
            this.rbUnitN.Checked = true;
            this.rbUnitN.CheckedChanged += new System.EventHandler(this.UnitSelection_Changed);
            // 
            // rbUnitKgf
            // 
            this.rbUnitKgf.Text = "kgf";
            this.rbUnitKgf.AutoSize = true;
            this.rbUnitKgf.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rbUnitKgf.Margin = new System.Windows.Forms.Padding(8, 5, 12, 5);
            this.rbUnitKgf.CheckedChanged += new System.EventHandler(this.UnitSelection_Changed);

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
            this.tlpMarkers.ResumeLayout(false);
            this.tlpMarkers.PerformLayout();
            this.grpMarkers.ResumeLayout(false);
            this.grpMarkers.PerformLayout();
            this.tlpForceUnit.ResumeLayout(false);
            this.tlpForceUnit.PerformLayout();
            this.grpForceUnit.ResumeLayout(false);
            this.grpForceUnit.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}