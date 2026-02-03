using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        // --- RAW DATA BACKUP ---
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

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true; 
            
            // --- GÖRÜNÜM DÜZELTMESİ (UI FIX) ---
            // GroupBox'ın boyutunu kod ile zorla büyütüyoruz ki alttaki butonlar sığsın.
            this.grpMarkers.Size = new Size(300, 140); // Yükseklik 90'dan 140'a çıkarıldı

            // 1. Kuvvet Sıfırlama Butonu
            rbSetZeroForce = new RadioButton();
            rbSetZeroForce.Text = "🔧 Zero Force";
            rbSetZeroForce.AutoSize = true;
            rbSetZeroForce.Location = new Point(15, 80); // Y koordinatı aşağı çekildi
            
            // 2. Açı Sıfırlama Butonu
            rbSetZeroAngle = new RadioButton();
            rbSetZeroAngle.Text = "📐 Zero Angle";
            rbSetZeroAngle.AutoSize = true;
            rbSetZeroAngle.Location = new Point(135, 80); // Y koordinatı aşağı çekildi

            // Gruba ekle
            this.grpMarkers.Controls.Add(rbSetZeroForce);
            this.grpMarkers.Controls.Add(rbSetZeroAngle);
            // -----------------------------------

            // Events
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.Paint += Canvas_Paint;
            canvas.Resize += (s, e) => canvas.Invalidate();
        }

        // LOAD JSON
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
                    
                    // --- YEDEKLEME ---
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

                        // --- SKALA AYARLARI (45 Derece & Orantısal Sıfır) ---
                        
                        // 1. KUVVET SKALASI (Max 30)
                        _maxForce = (float)Math.Max(30, maxF * 1.1);
                        _minForce = -(_maxForce / 6.0f); // Orantısal negatif alan
                        if (minF < _minForce) _minForce = (float)minF; 

                        // 2. AÇI SKALASI (BURASI DEĞİŞTİ: Max 45)
                        // Kullanıcı isteği: Varsayılan max açı 90 yerine 45 olsun.
                        _maxAngle = (float)Math.Max(45, maxA * 1.1); 
                        
                        _minAngle = -(_maxAngle / 6.0f); // Orantısal negatif alan
                        if (minA < _minAngle) _minAngle = (float)minA;
                        // ----------------------------------------------------
                        
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

        // DRAW
      private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float w = canvas.Width;
            float h = canvas.Height;
            float padLeft = 40, padRight = 40, padBot = 30, padTop = 40;
            RectangleF chartArea = new RectangleF(padLeft, padTop, w - padLeft - padRight, h - padTop - padBot);

            float ValToX(float t) => chartArea.Left + ((t - _minTime) / (_maxTime - _minTime)) * chartArea.Width;
            float ValToY_Force(float f) => chartArea.Bottom - ((f - _minForce) / (_maxForce - _minForce)) * chartArea.Height;
            float ValToY_Angle(float a) => chartArea.Bottom - ((a - _minAngle) / (_maxAngle - _minAngle)) * chartArea.Height;

            // --- GRID & ÇENTİKLER (YENİ EKLENEN KISIM) ---
            
            // 1. KUVVET EKSENİ (Sol - Kırmızı) - Her 5 birimde bir çizgi
            float stepF = 5.0f;
            // Başlangıç noktasını 5'in katına yuvarla
            float startF = (float)Math.Floor(_minForce / stepF) * stepF;
            
            for (float f = startF; f <= _maxForce; f += stepF)
            {
                float y = ValToY_Force(f);
                if (y < chartArea.Top || y > chartArea.Bottom) continue;

                // Yatay Grid Çizgisi (Hafif Kırmızı)
                using (Pen pGrid = new Pen(Color.FromArgb(20, 255, 0, 0), 1))
                    g.DrawLine(pGrid, chartArea.Left, y, chartArea.Right, y);

                // Etiket (0, 5, 10...)
                g.DrawString($"{f:F0}", new Font("Arial", 8), Brushes.Red, 2, y - 6);
            }

            // 2. AÇI EKSENİ (Sağ - Mavi) - Her 5 derecede bir çentik
            float stepA = 5.0f;
            float startA = (float)Math.Floor(_minAngle / stepA) * stepA;

            for (float a = startA; a <= _maxAngle; a += stepA)
            {
                float y = ValToY_Angle(a);
                if (y < chartArea.Top || y > chartArea.Bottom) continue;

                // Çentik (Sadece sağ kenarda 5px'lik çizgi)
                using (Pen pTick = new Pen(Color.Blue, 1))
                    g.DrawLine(pTick, chartArea.Right, y, chartArea.Right - 6, y);

                // Etiket (0°, 5°, 10°...)
                g.DrawString($"{a:F0}°", new Font("Arial", 8), Brushes.Blue, w - 35, y - 6);
            }
            // ----------------------------------------------

            // ZONES (Eski kod devam ediyor...)
            float xZ1 = ValToX(_zoneTimeStart);
            float xZ2 = ValToX(_zoneTimeEnd);
            using (SolidBrush bTime = new SolidBrush(Color.FromArgb(20, 0, 0, 255)))
                g.FillRectangle(bTime, xZ1, chartArea.Top, xZ2 - xZ1, chartArea.Height);
            using (Pen pTime = new Pen(Color.FromArgb(100, 0, 0, 255), 1) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pTime, xZ1, chartArea.Top, xZ1, chartArea.Bottom);
                g.DrawLine(pTime, xZ2, chartArea.Top, xZ2, chartArea.Bottom);
                
                string txtRot = "Rotation Zone";
                float txtX = xZ1 + ((xZ2 - xZ1) / 2) - (g.MeasureString(txtRot, this.Font).Width / 2);
                if(txtX < xZ1) txtX = xZ1;
                g.DrawString(txtRot, new Font("Arial", 11, FontStyle.Bold), Brushes.Blue, txtX, chartArea.Top + 2);
            }

            float yZ_Top = ValToY_Force(_zoneForceTop);
            float yZ_Bot = ValToY_Force(_zoneForceBottom);
            using (SolidBrush bForce = new SolidBrush(Color.FromArgb(30, 50, 50, 50)))
                g.FillRectangle(bForce, chartArea.Left, yZ_Top, chartArea.Width, yZ_Bot - yZ_Top);
            using (Pen pZone = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pZone, chartArea.Left, yZ_Top, chartArea.Right, yZ_Top);
                g.DrawLine(pZone, chartArea.Left, yZ_Bot, chartArea.Right, yZ_Bot);
                g.DrawString("Max Traction Zone", new Font("Arial", 11, FontStyle.Bold), Brushes.DimGray, chartArea.Left + 5, yZ_Top + 2);
            }

            // AXES (Eski statik yazıları kaldırdık, yukarıdaki döngü hallediyor)
            g.DrawRectangle(Pens.Black, Rectangle.Round(chartArea));
            using (Pen pAxis = new Pen(Color.Red, 2)) g.DrawLine(pAxis, chartArea.Left, chartArea.Top, chartArea.Left, chartArea.Bottom);
            using (Pen pAxis = new Pen(Color.Blue, 2)) g.DrawLine(pAxis, chartArea.Right, chartArea.Top, chartArea.Right, chartArea.Bottom);
            
            // DATA (Grafik Çizimi)
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

            // MARKERS (Aynen Kalıyor)
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
                    System.Drawing.Drawing2D.AdjustableArrowCap bigArrow = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 4);
                    pArrow.CustomEndCap = bigArrow;
                    g.DrawLine(pArrow, x, arrowY_Start, x, arrowY_End);
                }

                string valText = isForceLine ? $"{rawVal:F1}kg" : $"{rawVal:F0}°";
                string fullText = $"{title}\n{valText}";
                
                Font fontMarker = new Font("Segoe UI", 11, FontStyle.Bold);
                SizeF size = g.MeasureString(fullText, fontMarker);
                
                float textY = flipArrow ? arrowY_Start + 2 : arrowY_Start - size.Height - 2;
                float textX = x - (size.Width / 2);

                using(SolidBrush bBg = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.FillRectangle(bBg, textX - 2, textY - 2, size.Width + 4, size.Height + 4);
                g.DrawString(fullText, fontMarker, new SolidBrush(c), textX, textY);
            }

            DrawFancyMarker(_idxTraStart, Color.Green, "Tra. Start", true);
            DrawFancyMarker(_idxRotStart, Color.Orange, "Rot. Start", false); 
            DrawFancyMarker(_idxRotPeak, Color.DarkBlue, "Rot. Peak", false);
            DrawFancyMarker(_idxManip, Color.Purple, "Manip.", true);
        }

        // MOUSE
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

        // ANALYSIS REPORT
        private void btnAnalyze_Click(object? sender, EventArgs e)
        {
            if (_idxTraStart < 0 || _idxRotStart < 0 || _idxRotPeak < 0 || _idxManip < 0)
            {
                MessageBox.Show("Please place all 4 markers.");
                return;
            }

            double fManip = _forceData[_idxManip];
            double angPeak = _angleData[_idxRotPeak];
            double tRotStart = _timeData[_idxRotStart];
            double tRotPeak = _timeData[_idxRotPeak];
            double tManip = _timeData[_idxManip];

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- MANIPULATION ANALYSIS ---");
            sb.AppendLine($"Traction Start: {_timeData[_idxTraStart]:F2}s");
            sb.AppendLine($"Rotation Duration: {(tRotPeak - tRotStart):F2}s");
            sb.AppendLine($"Max Rotation: {angPeak:F1}°");
            sb.AppendLine($"Manipulation Force: {fManip:F1} kgf");
            sb.AppendLine("");

            if (tManip < tRotPeak) sb.AppendLine("[!] WARNING: Manipulation occurred BEFORE full rotation!");
            else sb.AppendLine("[OK] Sequence Correct (Rotation -> Manipulation)");

            if (fManip < _zoneForceBottom) sb.AppendLine("[X] FORCE LOW (Below Zone)");
            else if (fManip > (_zoneForceTop + 2.0)) sb.AppendLine("[X] FORCE HIGH (Above Zone)");
            else sb.AppendLine("[OK] Force in Target Zone");

            MessageBox.Show(sb.ToString());
        }
    }

    public class ManipulationRecord
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public string StudentName { get; set; } = "Student";
        public int SamplingRate { get; set; } = 1000;
        public List<double> Time { get; set; } = new List<double>();
        public List<double> Force { get; set; } = new List<double>();
        public List<double> Angle { get; set; } = new List<double>();
    }
}