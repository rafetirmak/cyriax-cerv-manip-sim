using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging; // Resim kaydı için gerekli
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.Json; 

namespace CervicalAnalyzer
{
    public partial class Form1 : Form
    {
        // --- DATA ---
        private List<double> _timeData = new List<double>();
        private List<double> _forceData = new List<double>();
        private List<double> _angleData = new List<double>();

        // --- RAW DATA BACKUP (Sıfırlama işlemleri için) ---
        private List<double> _rawForce = new List<double>();
        private List<double> _rawAngle = new List<double>();

        // --- 4 MARKERS ---
        private int _idxTraStart = -1; 
        private int _idxRotStart = -1; 
        private int _idxRotPeak = -1;  
        private int _idxManip = -1;    

        // --- UI CONTROLS (Manual) ---
        private RadioButton rbSetZeroForce;
        private RadioButton rbSetZeroAngle;

        // --- SCALES ---
        private float _minTime = 0, _maxTime = 10;
        private float _minForce = 0, _maxForce = 50;
        private float _minAngle = 0, _maxAngle = 120;

        // --- ZONES ---
        private float _zoneForceTop = 12.0f;
        private float _zoneForceBottom = 8.0f;
        private float _zoneTimeStart = 3.0f;
        private float _zoneTimeEnd = 5.0f;

        // --- MOUSE ---
        private enum DragMode { None, ForceTop, ForceBottom, TimeLeft, TimeRight, PanTime }
        
        private DragMode _currentDrag = DragMode.None;
        private Point _lastMousePos;

        // --- OPTIMIZATION: CACHED FONTS ---
        private Font _fontAxis;      
        private Font _fontZone;      
        private Font _fontMarker;    

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true; 
            
            // --- FONT OPTIMIZATION ---
            _fontAxis = new Font("Arial", 8);
            _fontZone = new Font("Arial", 11, FontStyle.Bold);
            _fontMarker = new Font("Segoe UI", 11, FontStyle.Bold);

            // --- UI: ZERO SETTING BUTTONS ---
            rbSetZeroForce = new RadioButton();
            rbSetZeroForce.Text = "🔧 Zero Force";
            rbSetZeroForce.AutoSize = true;
            rbSetZeroForce.Location = new Point(15, 80); 
            
            rbSetZeroAngle = new RadioButton();
            rbSetZeroAngle.Text = "📐 Zero Angle";
            rbSetZeroAngle.AutoSize = true;
            rbSetZeroAngle.Location = new Point(135, 80); 

            this.grpMarkers.Controls.Add(rbSetZeroForce);
            this.grpMarkers.Controls.Add(rbSetZeroAngle);

            // Events
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.Paint += Canvas_Paint;
            canvas.Resize += (s, e) => canvas.Invalidate();
        }

        // ---------------------------------------------------------
        // 1. DATA LOADING
        // ---------------------------------------------------------
        private void btnLoad_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON Files|*.json";
                if (ofd.ShowDialog() == DialogResult.OK) LoadJson(ofd.FileName);
            }
        }

        private void LoadJson(string path)
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var record = JsonSerializer.Deserialize<ManipulationRecord>(jsonContent, options);

                if (record != null && record.Force != null && record.Force.Count > 0)
                {
                    _forceData = record.Force ?? new List<double>();
                    _angleData = record.Angle ?? new List<double>();
                    
                    // Yedekle
                    _rawForce = new List<double>(_forceData);
                    _rawAngle = new List<double>(_angleData);

                    if (record.Time != null && record.Time.Count == _forceData.Count)
                        _timeData = record.Time;
                    else
                    {
                        _timeData = new List<double>();
                        double rate = (record.SamplingRate > 0) ? record.SamplingRate : 1000.0;
                        for (int i = 0; i < _forceData.Count; i++) _timeData.Add((double)i / rate);
                    }

                    if (_timeData.Count > 0)
                    {
                        _minTime = (float)_timeData.First();
                        _maxTime = (float)_timeData.Last();

                        double minF = _forceData.Count > 0 ? _forceData.Min() : 0;
                        double maxF = _forceData.Count > 0 ? _forceData.Max() : 50;
                        double minA = _angleData.Count > 0 ? _angleData.Min() : 0;
                        double maxA = _angleData.Count > 0 ? _angleData.Max() : 90;

                        // Skala Ayarları
                        _maxForce = (float)Math.Max(30, maxF * 1.1);
                        _minForce = -(_maxForce / 6.0f); 
                        if (minF < _minForce) _minForce = (float)minF; 

                        _maxAngle = (float)Math.Max(45, maxA * 1.1); 
                        _minAngle = -(_maxAngle / 6.0f); 
                        if (minA < _minAngle) _minAngle = (float)minA;
                        
                        _zoneTimeStart = _minTime + (_maxTime - _minTime) * 0.4f;
                        _zoneTimeEnd = _minTime + (_maxTime - _minTime) * 0.6f;
                    }
                    
                    _idxTraStart = -1; _idxRotStart = -1; _idxRotPeak = -1; _idxManip = -1;
                    rbTraStart.Checked = true;

                    canvas.Invalidate();
                    this.Text = $"Analysis - {record.Date:g}";
                }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        // ---------------------------------------------------------
        // 2. DRAWING LOGIC (REFACTORED)
        // ---------------------------------------------------------
        
        // Ekran çizimi
        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            DrawChart(e.Graphics, canvas.Width, canvas.Height);
        }

        // Ortak Çizim Metodu (Hem Ekran Hem PNG Kaydı İçin)
        private void DrawChart(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            // Arka planı temizle (PNG kaydı için kritik)
            g.Clear(Color.White);

            float padLeft = 40, padRight = 40, padBot = 30, padTop = 40;
            RectangleF chartArea = new RectangleF(padLeft, padTop, w - padLeft - padRight, h - padTop - padBot);

            float ValToX(float t) => chartArea.Left + ((t - _minTime) / (_maxTime - _minTime)) * chartArea.Width;
            float ValToY_Force(float f) => chartArea.Bottom - ((f - _minForce) / (_maxForce - _minForce)) * chartArea.Height;
            float ValToY_Angle(float a) => chartArea.Bottom - ((a - _minAngle) / (_maxAngle - _minAngle)) * chartArea.Height;

            // A. GRID & EKSENLER
            // ------------------
            // 1. Kuvvet (Kırmızı)
            float stepF = 5.0f;
            float startF = (float)Math.Floor(_minForce / stepF) * stepF;
            for (float f = startF; f <= _maxForce; f += stepF)
            {
                float y = ValToY_Force(f);
                if (y < chartArea.Top || y > chartArea.Bottom) continue;

                using (Pen pGrid = new Pen(Color.FromArgb(20, 255, 0, 0), 1))
                    g.DrawLine(pGrid, chartArea.Left, y, chartArea.Right, y);

                g.DrawString($"{f:F0}", _fontAxis, Brushes.Red, 2, y - 6);
            }

            // 2. Açı (Mavi)
            float stepA = 5.0f;
            float startA = (float)Math.Floor(_minAngle / stepA) * stepA;
            for (float a = startA; a <= _maxAngle; a += stepA)
            {
                float y = ValToY_Angle(a);
                if (y < chartArea.Top || y > chartArea.Bottom) continue;

                using (Pen pTick = new Pen(Color.Blue, 1))
                    g.DrawLine(pTick, chartArea.Right, y, chartArea.Right - 6, y);

                g.DrawString($"{a:F0}°", _fontAxis, Brushes.Blue, w - 35, y - 6);
            }

            // B. ZONES (Bölgeler)
            // -------------------
            float xZ1 = ValToX(_zoneTimeStart);
            float xZ2 = ValToX(_zoneTimeEnd);
            
            // Rotation Zone
            using (SolidBrush bTime = new SolidBrush(Color.FromArgb(20, 0, 0, 255)))
                g.FillRectangle(bTime, xZ1, chartArea.Top, xZ2 - xZ1, chartArea.Height);
            
            using (Pen pTime = new Pen(Color.FromArgb(100, 0, 0, 255), 1) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pTime, xZ1, chartArea.Top, xZ1, chartArea.Bottom);
                g.DrawLine(pTime, xZ2, chartArea.Top, xZ2, chartArea.Bottom);
                
                string txtRot = "Rotation Zone";
                float txtX = xZ1 + ((xZ2 - xZ1) / 2) - (g.MeasureString(txtRot, _fontZone).Width / 2);
                if(txtX < xZ1) txtX = xZ1;
                g.DrawString(txtRot, _fontZone, Brushes.Blue, txtX, chartArea.Top + 2);
            }

            // Force Zone
            float yZ_Top = ValToY_Force(_zoneForceTop);
            float yZ_Bot = ValToY_Force(_zoneForceBottom);
            using (SolidBrush bForce = new SolidBrush(Color.FromArgb(30, 50, 50, 50)))
                g.FillRectangle(bForce, chartArea.Left, yZ_Top, chartArea.Width, yZ_Bot - yZ_Top);
            
            using (Pen pZone = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pZone, chartArea.Left, yZ_Top, chartArea.Right, yZ_Top);
                g.DrawLine(pZone, chartArea.Left, yZ_Bot, chartArea.Right, yZ_Bot);
                g.DrawString("Max Traction Zone", _fontZone, Brushes.DimGray, chartArea.Left + 5, yZ_Top + 2);
            }

            // Çerçeve
            g.DrawRectangle(Pens.Black, Rectangle.Round(chartArea));
            using (Pen pAxis = new Pen(Color.Red, 2)) g.DrawLine(pAxis, chartArea.Left, chartArea.Top, chartArea.Left, chartArea.Bottom);
            using (Pen pAxis = new Pen(Color.Blue, 2)) g.DrawLine(pAxis, chartArea.Right, chartArea.Top, chartArea.Right, chartArea.Bottom);

            // C. DATA PLOTTING
            // ----------------
            if (_timeData.Count > 1)
            {
                using (Pen pF = new Pen(Color.Red, 2))
                using (Pen pA = new Pen(Color.Blue, 2))
                {
                    var state = g.Save(); 
                    g.SetClip(chartArea);
                    for (int i = 0; i < _timeData.Count - 1; i++)
                    {
                        float x1 = ValToX((float)_timeData[i]);
                        float x2 = ValToX((float)_timeData[i+1]);
                        g.DrawLine(pF, x1, ValToY_Force((float)_forceData[i]), x2, ValToY_Force((float)_forceData[i+1]));
                        g.DrawLine(pA, x1, ValToY_Angle((float)_angleData[i]), x2, ValToY_Angle((float)_angleData[i+1]));
                    }
                    g.Restore(state);
                }
            }

            // D. MARKERS
            // ----------
            void DrawFancyMarker(int idx, Color c, string title, bool isForceLine)
            {
                if(idx < 0 || idx >= _timeData.Count) return;

                float x = ValToX((float)_timeData[idx]);
                float rawVal = isForceLine ? (float)_forceData[idx] : (float)_angleData[idx];
                float y = isForceLine ? ValToY_Force(rawVal) : ValToY_Angle(rawVal);

                using(Pen pDash = new Pen(Color.FromArgb(150, c), 1) { DashStyle = DashStyle.Dot })
                    g.DrawLine(pDash, x, chartArea.Top, x, chartArea.Bottom);

                float r = 5; 
                using(SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, 2*r, 2*r);

                float arrowHeight = 30;
                float arrowY_Start = y - arrowHeight - 5;
                float arrowY_End = y - 5;
                bool flipArrow = (y < chartArea.Top + 50); 
                if(flipArrow) { arrowY_Start = y + arrowHeight + 5; arrowY_End = y + 5; }

                using(Pen pArrow = new Pen(c, 2))
                {
                    AdjustableArrowCap bigArrow = new AdjustableArrowCap(4, 4);
                    pArrow.CustomEndCap = bigArrow;
                    g.DrawLine(pArrow, x, arrowY_Start, x, arrowY_End);
                }

                string valText = isForceLine ? $"{rawVal:F1}kg" : $"{rawVal:F0}°";
                string fullText = $"{title}\n{valText}";
                
                SizeF size = g.MeasureString(fullText, _fontMarker);
                
                float textY = flipArrow ? arrowY_Start + 2 : arrowY_Start - size.Height - 2;
                float textX = x - (size.Width / 2);

                using(SolidBrush bBg = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.FillRectangle(bBg, textX - 2, textY - 2, size.Width + 4, size.Height + 4);
                
                g.DrawString(fullText, _fontMarker, new SolidBrush(c), textX, textY);
            }

            DrawFancyMarker(_idxTraStart, Color.Green, "Tra. Start", true);
            DrawFancyMarker(_idxRotStart, Color.Orange, "Rot. Start", false); 
            DrawFancyMarker(_idxRotPeak, Color.DarkBlue, "Rot. Peak", false);
            DrawFancyMarker(_idxManip, Color.Purple, "Manip.", true);
        }

        // ---------------------------------------------------------
        // 3. MOUSE INTERACTION
        // ---------------------------------------------------------
        private void Canvas_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_timeData.Count == 0) return;

            float padLeft = 40, padRight = 40, padBot = 30, padTop = 40;
            RectangleF area = new RectangleF(padLeft, padTop, canvas.Width - padLeft - padRight, canvas.Height - padTop - padBot);

            float ForceToY(float f) => area.Bottom - ((f - _minForce) / (_maxForce - _minForce)) * area.Height;
            float TimeToX(float t) => area.Left + ((t - _minTime) / (_maxTime - _minTime)) * area.Width;
            float XToTime(float x) => _minTime + ((x - area.Left) / area.Width) * (_maxTime - _minTime);

            float tol = 6.0f; 

            float yTop = ForceToY(_zoneForceTop);
            float yBot = ForceToY(_zoneForceBottom);
            float xLeft = TimeToX(_zoneTimeStart);
            float xRight = TimeToX(_zoneTimeEnd);

            if (Math.Abs(e.Y - yTop) < tol) _currentDrag = DragMode.ForceTop;
            else if (Math.Abs(e.Y - yBot) < tol) _currentDrag = DragMode.ForceBottom;
            else if (Math.Abs(e.X - xLeft) < tol) _currentDrag = DragMode.TimeLeft;
            else if (Math.Abs(e.X - xRight) < tol) _currentDrag = DragMode.TimeRight;
            else if (e.X > xLeft && e.X < xRight && e.Y > area.Top && e.Y < area.Top + 30)
            {
                _currentDrag = DragMode.PanTime;
                _lastMousePos = e.Location;
            }
            else
            {
                float clickedTime = XToTime(e.X);
                int bestIdx = 0; double minDiff = double.MaxValue;
                for (int i = 0; i < _timeData.Count; i++)
                {
                    double diff = Math.Abs(_timeData[i] - clickedTime);
                    if (diff < minDiff) { minDiff = diff; bestIdx = i; }
                }

                // 1. FORCE RESET
                if (rbSetZeroForce.Checked)
                {
                    if (_rawForce == null || _rawForce.Count == 0) { MessageBox.Show("Please Reload JSON."); return; }
                    
                    int countToAverage = Math.Min(50, _rawForce.Count - bestIdx);
                    if (countToAverage > 0)
                    {
                        double forceOffset = _rawForce.GetRange(bestIdx, countToAverage).Average();
                        for (int i = 0; i < _forceData.Count; i++)
                        {
                            if (i < _rawForce.Count) 
                                _forceData[i] = _rawForce[i] - forceOffset;
                        }
                        MessageBox.Show($"Force Baseline Reset!\nTime: {clickedTime:F2}s\nOffset: {forceOffset:F2} kg");
                    }
                }
                // 2. ANGLE RESET
                else if (rbSetZeroAngle.Checked)
                {
                    if (_rawAngle == null || _rawAngle.Count == 0) { MessageBox.Show("Please Reload JSON."); return; }

                    int countToAverage = Math.Min(50, _rawAngle.Count - bestIdx);
                    if (countToAverage > 0)
                    {
                        double angleOffset = _rawAngle.GetRange(bestIdx, countToAverage).Average();
                        for (int i = 0; i < _angleData.Count; i++)
                        {
                            if (i < _rawAngle.Count)
                                _angleData[i] = _rawAngle[i] - angleOffset;
                        }
                        MessageBox.Show($"Angle Baseline Reset!\nTime: {clickedTime:F2}s\nOffset: {angleOffset:F1}°");
                    }
                }
                else if (rbTraStart.Checked) _idxTraStart = bestIdx;
                else if (rbRotStart.Checked) _idxRotStart = bestIdx;
                else if (rbRotPeak.Checked) _idxRotPeak = bestIdx;
                else if (rbManip.Checked) _idxManip = bestIdx;
            }
            canvas.Invalidate();
        }

        private void Canvas_MouseMove(object? sender, MouseEventArgs e)
        {
            float padLeft = 40, padRight = 40, padBot = 30, padTop = 40;
            RectangleF area = new RectangleF(padLeft, padTop, canvas.Width - padLeft - padRight, canvas.Height - padTop - padBot);

            float YToForce(float y) => _minForce + ((area.Bottom - y) / area.Height) * (_maxForce - _minForce);
            float XToTime(float x) => _minTime + ((x - area.Left) / area.Width) * (_maxTime - _minTime);
            float ForceToY(float f) => area.Bottom - ((f - _minForce) / (_maxForce - _minForce)) * area.Height;
            float TimeToX(float t) => area.Left + ((t - _minTime) / (_maxTime - _minTime)) * area.Width;

            if (e.Button == MouseButtons.Left && _currentDrag != DragMode.None)
            {
                float valF = YToForce(e.Y);
                float valT = XToTime(e.X);

                switch (_currentDrag)
                {
                    case DragMode.ForceTop: if (valF > _zoneForceBottom) _zoneForceTop = valF; break;
                    case DragMode.ForceBottom: if (valF < _zoneForceTop) _zoneForceBottom = valF; break;

                    case DragMode.TimeLeft: if (valT < _zoneTimeEnd) _zoneTimeStart = valT; break;
                    case DragMode.TimeRight: if (valT > _zoneTimeStart) _zoneTimeEnd = valT; break;
                    case DragMode.PanTime:
                        float dt = XToTime(e.X) - XToTime(_lastMousePos.X);
                        _zoneTimeStart += dt; _zoneTimeEnd += dt;
                        _lastMousePos = e.Location; break;
                }
                canvas.Invalidate();
            }

            float tol = 6.0f;
            if (Math.Abs(e.Y - ForceToY(_zoneForceTop)) < tol || Math.Abs(e.Y - ForceToY(_zoneForceBottom)) < tol) 
                canvas.Cursor = Cursors.SizeNS;
            else if (Math.Abs(e.X - TimeToX(_zoneTimeStart)) < tol || Math.Abs(e.X - TimeToX(_zoneTimeEnd)) < tol) 
                canvas.Cursor = Cursors.SizeWE;
            else if (e.X > TimeToX(_zoneTimeStart) && e.X < TimeToX(_zoneTimeEnd) && e.Y < area.Top + 30) 
                canvas.Cursor = Cursors.Hand;
            else canvas.Cursor = Cursors.Default;
        }

        private void Canvas_MouseUp(object? sender, MouseEventArgs e)
        {
            _currentDrag = DragMode.None;
        }

        // ---------------------------------------------------------
        // 4. REPORTING & SAVING (ANALYZER)
        // ---------------------------------------------------------
        private void btnAnalyze_Click(object? sender, EventArgs e)
        {
            if (_idxTraStart < 0 || _idxRotStart < 0 || _idxRotPeak < 0 || _idxManip < 0)
            {
                MessageBox.Show("Please place all 4 markers.");
                return;
            }

            // A. HESAPLAMALAR
            double fManip = _forceData[_idxManip];
            double angPeak = _angleData[_idxRotPeak];
            double tTraStart = _timeData[_idxTraStart];
            double tRotStart = _timeData[_idxRotStart];
            double tRotPeak = _timeData[_idxRotPeak];
            double tManip = _timeData[_idxManip];

            bool seqCorrect = (tManip >= tRotPeak); // Manipülasyon, rotasyon zirvesinden sonra mı?
            string forceStatus = "OK";
            if (fManip < _zoneForceBottom) forceStatus = "LOW";
            else if (fManip > (_zoneForceTop + 2.0)) forceStatus = "HIGH";

            // B. MESAJ KUTUSU GÖSTERİMİ
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- MANIPULATION ANALYSIS ---");
            sb.AppendLine($"Traction Start: {tTraStart:F2}s");
            sb.AppendLine($"Rotation Duration: {(tRotPeak - tRotStart):F2}s");
            sb.AppendLine($"Max Rotation: {angPeak:F1}°");
            sb.AppendLine($"Manipulation Force: {fManip:F1} kgf");
            sb.AppendLine("");
            sb.AppendLine(seqCorrect ? "[OK] Sequence Correct" : "[!] WARNING: Manip before rotation peak!");
            sb.AppendLine($"Force Status: {forceStatus}");
            
           // MessageBox.Show(sb.ToString(), "Report");

            // C. KAYDETME İŞLEMİ (JSON + PNG)
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Save Analysis Report & Chart";
                sfd.Filter = "JSON Report|*.json";
                sfd.FileName = $"Analysis_{DateTime.Now:yyyyMMdd_HHmm}";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string jsonPath = sfd.FileName;
                    string imagePath = jsonPath.Replace(".json", ".png");

                    try
                    {
                        // 1. JSON Kaydetme
                        var result = new AnalysisResult
                        {
                            AnalysisDate = DateTime.Now,
                            TractionStart_Time = tTraStart,
                            RotationStart_Time = tRotStart,
                            RotationPeak_Time = tRotPeak,
                            Manipulation_Time = tManip,
                            Manipulation_Force = fManip,
                            Max_Rotation_Angle = angPeak,
                            Rotation_Duration = tRotPeak - tRotStart,
                            IsSequenceCorrect = seqCorrect,
                            ForceZoneStatus = forceStatus
                        };

                        string jsonString = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(jsonPath, jsonString);

                        // 2. PNG Kaydetme (DrawChart Metodunu Kullanarak)
                        // Ekrandaki canvas boyutunda bir Bitmap oluştur
                        using (Bitmap bmp = new Bitmap(canvas.Width, canvas.Height))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                // Aynı çizim fonksiyonunu çağır!
                                DrawChart(g, canvas.Width, canvas.Height);
                            }
                            bmp.Save(imagePath, ImageFormat.Png);
                        }

                        MessageBox.Show($"Saved!\nJSON: {Path.GetFileName(jsonPath)}\nIMG: {Path.GetFileName(imagePath)}", "Success");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving files: " + ex.Message);
                    }
                }
            }
        }
    }

    // --- HELPER CLASSES ---

    // JSON Yükleme Sınıfı
    public class ManipulationRecord
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public string StudentName { get; set; } = "Student";
        public int SamplingRate { get; set; } = 1000;
        public List<double> Time { get; set; } = new List<double>();
        public List<double> Force { get; set; } = new List<double>();
        public List<double> Angle { get; set; } = new List<double>();
    }

    // JSON Kaydetme Sınıfı (Sonuçlar)
    public class AnalysisResult
    {
        public DateTime AnalysisDate { get; set; }
        public double TractionStart_Time { get; set; }
        public double RotationStart_Time { get; set; }
        public double RotationPeak_Time { get; set; }
        public double Manipulation_Time { get; set; }
        public double Manipulation_Force { get; set; }
        public double Max_Rotation_Angle { get; set; }
        public double Rotation_Duration { get; set; }
        public bool IsSequenceCorrect { get; set; }
        public string ForceZoneStatus { get; set; } = "";
    }
}