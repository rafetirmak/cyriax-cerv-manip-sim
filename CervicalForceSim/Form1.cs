using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using MccDaq;

namespace CervicalForceSim
{
    public partial class Form1 : Form
    {
        // ---- Konfigürasyon ----
        private const int BoardNum = 0;
        private const int LowChan = 0;
        private const int HighChan = 1;
        private const MccDaq.Range DaqRange = MccDaq.Range.Bip10Volts;

        // Kayıt örnekleme (per channel)
        private const int SampleRate = 1000;
        private const int DurationSec = 10;

        // Ekran çizim hedefi (downsample)
        private const int DisplayRate = 500;

        // Manuel eksen aralıkları (SENİN İSTEDİĞİN)
        private const float ForceMin = -5f;
        private const float ForceMax = 20f;

        private const float AngleMin = -40f;
        private const float AngleMax = 160f;


        private const bool InvertAngle = true; // açı yönünü ters çevir (gerekliyse true)

        // ---- Buffer ----
        private int _totalBufferSize; // TOTAL points (all channels)
        private IntPtr _memHandle = IntPtr.Zero;
        private int _lastScanIndex = 0;
        private bool _isAcquiring = false;
        private System.Windows.Forms.Timer _plotTimer;
        private int _sampleRateProxy = SampleRate;

        // ---- Tam çözünürlük (JSON) ----
        private readonly List<double> _timeLog = new();
        private readonly List<double> _forceLog = new();
        private readonly List<double> _angleLog = new();

        // ---- Ekran için downsample ----
        private readonly List<PointF> _forceData = new();
        private readonly List<PointF> _angleData = new();

        // ---- Kalibrasyon parametreleri (Volt -> birim) ----
        private double _forceSlope = 10.0;
        private double _forceOffset = 0.0;
        private double _angleSlope = 72.0;
        private double _angleOffset = 0.0;

        // ---- Baseline (zero) kalibrasyonu: ölçümden düşülecek değerler ----
        private double _forceZero = 0.0; // kgf
        private double _angleZero = 0.0; // deg

        // ---- Filtreler ----
        private ButterworthFilter? _forceFilter;
        private ButterworthFilter? _angleFilter;

        // ---- Display decimation ----
        private int _displayDecimation = 2; // 1000 -> 500 default

        private int NumChans => (HighChan - LowChan + 1);

        // ---- En son değerler (butonla "zero" almak için) ----
        private double _lastForceShown = 0.0;
        private double _lastAngleShown = 0.0;

        public Form1()
        {
            InitializeComponent();
            LoadCalibration();

            btnStop.Enabled = false;
            btnSaveCsv.Enabled = false;

            UpdateSamplingLabel();

            _plotTimer = new System.Windows.Forms.Timer();
            _plotTimer.Interval = 50;
            _plotTimer.Tick += OnTimerTick;

            numTargetForce.ValueChanged += (s, e) => canvas.Invalidate();

            if (btnZeroForce != null) btnZeroForce.Click += btnZeroForce_Click;
            if (btnZeroAngle != null) btnZeroAngle.Click += btnZeroAngle_Click;
        }

        private void btnStart_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_isAcquiring) return;

                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnSaveCsv.Enabled = false;
                lblStatus.Text = "Acquiring...";

                _forceData.Clear();
                _angleData.Clear();
                _timeLog.Clear();
                _forceLog.Clear();
                _angleLog.Clear();
                _lastScanIndex = 0;

                int numChans = NumChans;
                _totalBufferSize = SampleRate * DurationSec * numChans;

                if (_memHandle != IntPtr.Zero)
                    MccService.WinBufFreeEx(_memHandle);

                _memHandle = MccService.WinBufAllocEx(_totalBufferSize);
                if (_memHandle == IntPtr.Zero)
                    throw new Exception("Memory Alloc Failed");

                var board = new MccBoard(BoardNum);

                ScanOptions options = ScanOptions.Background | ScanOptions.ConvertData;

                _sampleRateProxy = SampleRate;

                int count = _totalBufferSize; // TOTAL points

                ErrorInfo err = board.AInScan(LowChan, HighChan, count, ref _sampleRateProxy, DaqRange, _memHandle, options);
                if (err.Value != ErrorInfo.ErrorCode.NoErrors)
                    throw new Exception("AInScan Error: " + err.Message);

                _forceFilter = new ButterworthFilter(_sampleRateProxy, 15.0);
                _angleFilter = new ButterworthFilter(_sampleRateProxy, 15.0);

                _displayDecimation = Math.Max(1, (int)Math.Round((double)_sampleRateProxy / DisplayRate));
                UpdateSamplingLabel();

                _isAcquiring = true;
                _plotTimer.Start();
            }
            catch (Exception ex)
            {
                StopAcquisition();
                MessageBox.Show("Start Error: " + ex.Message);
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_isAcquiring) return;

            var board = new MccBoard(BoardNum);
            short status;
            int curCount, curIndex;
            board.GetStatus(out status, out curCount, out curIndex, FunctionType.AiFunction);

            ReadAvailablePoints(curIndex);

            if (status != MccBoard.Running)
            {
                StopAcquisition();
                lblStatus.Text = "Done.";
                return;
            }

            canvas.Invalidate();
        }

        private void ReadAvailablePoints(int curIndex)
        {
            if (_memHandle == IntPtr.Zero) return;

            int numChans = NumChans;
            if (numChans <= 0) return;

            if (curIndex == _lastScanIndex) return;

            if (curIndex > _lastScanIndex)
            {
                int pointsToRead = curIndex - _lastScanIndex;
                pointsToRead -= (pointsToRead % numChans);

                if (pointsToRead <= 0) return;

                ReadAndProcess(_lastScanIndex, pointsToRead);
                _lastScanIndex += pointsToRead;
                return;
            }

            int tailPoints = _totalBufferSize - _lastScanIndex;
            tailPoints -= (tailPoints % numChans);
            if (tailPoints > 0)
            {
                ReadAndProcess(_lastScanIndex, tailPoints);
                _lastScanIndex = (_lastScanIndex + tailPoints) % _totalBufferSize;
            }

            int headPoints = curIndex;
            headPoints -= (headPoints % numChans);
            if (headPoints > 0)
            {
                ReadAndProcess(0, headPoints);
                _lastScanIndex = headPoints;
            }
        }

        private void ReadAndProcess(int startPoint, int pointsToRead)
        {
            if (pointsToRead <= 0) return;

            ushort[] chunk = new ushort[pointsToRead];
            MccService.WinBufToArray(_memHandle, chunk, startPoint, pointsToRead);
            ProcessChunk(chunk);
        }

        private void ProcessChunk(ushort[] chunk)
        {
            int numChans = NumChans;
            if (numChans <= 0) return;

            int scanCount = chunk.Length / numChans;
            if (scanCount <= 0) return;

            var board = new MccBoard(BoardNum);
            int startIndex = _timeLog.Count;

            for (int i = 0; i < scanCount; i++)
            {
                ushort rawForce = chunk[i * numChans + 0];
                ushort rawAngle = chunk[i * numChans + 1];

                float vForce, vAngle;
                board.ToEngUnits(DaqRange, rawForce, out vForce);
                board.ToEngUnits(DaqRange, rawAngle, out vAngle);

                double forceKgf = (_forceSlope * vForce) + _forceOffset;
                double angleDeg = (_angleSlope * vAngle) + _angleOffset;

                if (InvertAngle) angleDeg = -angleDeg;

                if (chkFilter.Checked)
                {
                    if (_forceFilter != null) forceKgf = _forceFilter.Filter(forceKgf);
                    if (_angleFilter != null) angleDeg = _angleFilter.Filter(angleDeg);
                }

                forceKgf -= _forceZero;
                angleDeg -= _angleZero;

                int globalSampleIndex = startIndex + i;
                float t = (float)globalSampleIndex / _sampleRateProxy;

                _timeLog.Add(t);
                _forceLog.Add(forceKgf);
                _angleLog.Add(angleDeg);

                if (_displayDecimation <= 1 || (globalSampleIndex % _displayDecimation == 0))
                {
                    _forceData.Add(new PointF(t, (float)forceKgf));
                    _angleData.Add(new PointF(t, (float)angleDeg));
                }

                _lastForceShown = forceKgf;
                _lastAngleShown = angleDeg;
            }

            txtForceVal.Text = _lastForceShown.ToString("F2");
            txtAngleVal.Text = _lastAngleShown.ToString("F1");
        }

        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float W = canvas.Width;
            float H = canvas.Height;

            float leftM = 55;
            float rightM = 55;
            float topM = 15;
            float bottomM = 35;

            float plotW = Math.Max(1, W - leftM - rightM);
            float plotH = Math.Max(1, H - topM - bottomM);

            // Izgara
            using (Pen pGrid = new Pen(Color.LightGray, 1))
            {
                for (int i = 0; i <= 10; i++)
                {
                    float y = topM + i * (plotH / 10.0f);
                    g.DrawLine(pGrid, leftM, y, leftM + plotW, y);

                    float x = leftM + i * (plotW / 10.0f);
                    g.DrawLine(pGrid, x, topM, x, topM + plotH);
                }
            }

            // Target zone (ForceMin/ForceMax'e göre)
            float targetForce = (float)numTargetForce.Value;
            float tol = 2.0f;

            float yZoneTop = topM + plotH - (((targetForce + tol - ForceMin) / (ForceMax - ForceMin)) * plotH);
            float yZoneBottom = topM + plotH - (((targetForce - tol - ForceMin) / (ForceMax - ForceMin)) * plotH);

            using (SolidBrush bZone = new SolidBrush(Color.FromArgb(50, 128, 128, 128)))
            {
                g.FillRectangle(bZone, leftM, yZoneTop, plotW, yZoneBottom - yZoneTop);
            }

            // Eksen çizgileri
            using (Pen pAxis = new Pen(Color.Gray, 2))
            {
                g.DrawLine(pAxis, leftM, topM, leftM, topM + plotH);                 // sol y
                g.DrawLine(pAxis, leftM + plotW, topM, leftM + plotW, topM + plotH); // sağ y
                g.DrawLine(pAxis, leftM, topM + plotH, leftM + plotW, topM + plotH); // x
            }

            // Force 0 çizgisi (daha net görünsün)
            float yZeroForce = topM + plotH - (((0f - ForceMin) / (ForceMax - ForceMin)) * plotH);
            using (Pen pZero = new Pen(Color.Black, 2))
            {
                g.DrawLine(pZero, leftM, yZeroForce, leftM + plotW, yZeroForce);
            }

            DrawAxes(g, leftM, rightM, topM, bottomM, plotW, plotH);

            if (_forceData.Count < 2) return;

            float maxTime = DurationSec;

            using (Pen pForce = new Pen(Color.Red, 2))
            using (Pen pAngle = new Pen(Color.Blue, 2))
            {
                for (int i = 1; i < _forceData.Count; i++)
                {
                    float x1 = leftM + (_forceData[i - 1].X / maxTime) * plotW;
                    float x2 = leftM + (_forceData[i].X / maxTime) * plotW;

                    if (x2 > leftM + plotW) continue;

                    float yF1 = topM + plotH - (((_forceData[i - 1].Y - ForceMin) / (ForceMax - ForceMin)) * plotH);
                    float yF2 = topM + plotH - (((_forceData[i].Y     - ForceMin) / (ForceMax - ForceMin)) * plotH);

                    float yA1 = topM + plotH - (((_angleData[i - 1].Y - AngleMin) / (AngleMax - AngleMin)) * plotH);
                    float yA2 = topM + plotH - (((_angleData[i].Y     - AngleMin) / (AngleMax - AngleMin)) * plotH);

                    g.DrawLine(pForce, x1, yF1, x2, yF2);
                    g.DrawLine(pAngle, x1, yA1, x2, yA2);
                }
            }
        }

        private void DrawAxes(Graphics g, float leftM, float rightM, float topM, float bottomM, float plotW, float plotH)
        {
            using var f = new Font("Segoe UI", 9);
            using var b = new SolidBrush(Color.DimGray);

            // Left Y label (Force)
            g.DrawString("Force (kgf)", f, b, 5, topM - 2);

            // Right Y label (Angle)
            var angleLabel = "Angle (°)";
            var sz = g.MeasureString(angleLabel, f);
            g.DrawString(angleLabel, f, b, leftM + plotW + (rightM - sz.Width) / 2, topM - 2);

            // X label (Time)
            var xLabel = "Time (s)";
            var xSz = g.MeasureString(xLabel, f);
            g.DrawString(xLabel, f, b, leftM + (plotW - xSz.Width) / 2, topM + plotH + 8);

            // Y ticks: ForceMin..ForceMax / AngleMin..AngleMax
            for (int i = 0; i <= 5; i++)
            {
                float frac = i / 5f;
                float y = topM + plotH - frac * plotH;

                float forceVal = ForceMin + frac * (ForceMax - ForceMin);
                string ft = forceVal.ToString("0");
                var fsz = g.MeasureString(ft, f);
                g.DrawString(ft, f, b, leftM - 6 - fsz.Width, y - fsz.Height / 2);

                float angVal = AngleMin + frac * (AngleMax - AngleMin);
                string at = angVal.ToString("0");
                g.DrawString(at, f, b, leftM + plotW + 6, y - fsz.Height / 2);
            }

            // X ticks: 0..DurationSec
            for (int i = 0; i <= 5; i++)
            {
                float frac = i / 5f;
                float x = leftM + frac * plotW;
                float t = frac * DurationSec;
                string tt = t.ToString("0");
                var tsz = g.MeasureString(tt, f);
                g.DrawString(tt, f, b, x - tsz.Width / 2, topM + plotH + 18);
            }
        }

        private void btnZeroForce_Click(object? sender, EventArgs e)
        {
            _forceZero += _lastForceShown;
            SaveCalibration();
        }

        private void btnZeroAngle_Click(object? sender, EventArgs e)
        {
            _angleZero += _lastAngleShown;
            SaveCalibration();
        }

        private void btnSaveCsv_Click(object sender, EventArgs e)
        {
            if (_timeLog.Count == 0) return;

            SaveFileDialog sfd = new SaveFileDialog { Filter = "JSON Dosyası|*.json" };
            sfd.FileName = "Manipulasyon_" + DateTime.Now.ToString("yyyyMMdd_HHmm");

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var record = new ManipulationRecord
                {
                    SamplingRate = SampleRate,
                    ActualSamplingRate = _sampleRateProxy,
                    DisplaySamplingRate = DisplayRate,
                    ForceZero = _forceZero,
                    AngleZero = _angleZero,
                    Time = _timeLog,
                    Force = _forceLog,
                    Angle = _angleLog
                };

                File.WriteAllText(
                    sfd.FileName,
                    JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true })
                );

                MessageBox.Show("Kayıt Başarılı.");
            }
        }

        private void btnStop_Click(object? sender, EventArgs e) => StopAcquisition();

        private void StopAcquisition()
        {
            _isAcquiring = false;
            _plotTimer.Stop();

            try { new MccBoard(BoardNum).StopBackground(FunctionType.AiFunction); } catch { }

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnSaveCsv.Enabled = true;
            lblStatus.Text = "Stopped.";
        }

        private string GetCalibrationPath()
            => Path.Combine(AppContext.BaseDirectory, "calibration.json");

        private void LoadCalibration()
        {
            string path = GetCalibrationPath();
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<CalibrationConfig>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (config == null) return;

                _forceSlope = config.Force.Slope;
                _forceOffset = config.Force.Offset;

                _angleSlope = config.Angle.Slope;
                _angleOffset = config.Angle.Offset;

                _forceZero = config.Force.Zero;
                _angleZero = config.Angle.Zero;

                numTargetForce.Value = config.DefaultTargetForce;
            }
            catch { }
        }

        private void SaveCalibration()
        {
            try
            {
                var config = new CalibrationConfig
                {
                    Force = new SensorConfig { Slope = _forceSlope, Offset = _forceOffset, Zero = _forceZero },
                    Angle = new SensorConfig { Slope = _angleSlope, Offset = _angleOffset, Zero = _angleZero },
                    DefaultTargetForce = numTargetForce.Value
                };

                File.WriteAllText(
                    GetCalibrationPath(),
                    JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
                );
            }
            catch { }
        }

        private void UpdateSamplingLabel()
        {
            if (lblSampling == null) return;
            lblSampling.Text = $"Sampling: {SampleRate} Hz | Actual: {_sampleRateProxy} Hz | Display: {DisplayRate} Hz";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopAcquisition();
            SaveCalibration();

            if (_memHandle != IntPtr.Zero)
                MccService.WinBufFreeEx(_memHandle);

            base.OnFormClosing(e);
        }
    }

    // ---- Helper classes ----
    public class ButterworthFilter
    {
        private readonly double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public ButterworthFilter(double sampleRate, double cutoffFrequency)
        {
            double samplingPeriod = 1.0 / sampleRate;
            double wc = 2.0 * Math.PI * cutoffFrequency;
            double k = wc / Math.Tan(wc * samplingPeriod / 2.0);
            double denom = k * k + Math.Sqrt(2) * k * wc + wc * wc;
            _b0 = (wc * wc) / denom;
            _b1 = 2 * _b0;
            _b2 = _b0;
            _a1 = 2 * (wc * wc - k * k) / denom;
            _a2 = (k * k - Math.Sqrt(2) * k * wc + wc * wc) / denom;
        }

        public double Filter(double input)
        {
            double output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = input;
            _y2 = _y1; _y1 = output;
            return output;
        }
    }

    public class CalibrationConfig
    {
        public SensorConfig Force { get; set; } = new SensorConfig();
        public SensorConfig Angle { get; set; } = new SensorConfig();
        public decimal DefaultTargetForce { get; set; } = 22;
    }

    public class SensorConfig
    {
        public double Slope { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public double Zero { get; set; } = 0.0;
    }

    public class ManipulationRecord
    {
        public DateTime Date { get; set; } = DateTime.Now;

        public int SamplingRate { get; set; }
        public int ActualSamplingRate { get; set; }
        public int DisplaySamplingRate { get; set; }

        public double ForceZero { get; set; }
        public double AngleZero { get; set; }

        public List<double> Time { get; set; } = new();
        public List<double> Force { get; set; } = new();
        public List<double> Angle { get; set; } = new();
    }
}
