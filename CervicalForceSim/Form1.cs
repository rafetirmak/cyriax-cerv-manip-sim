using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MccDaq;
using System.Text.Json;

namespace CervicalForceSim
{
    public partial class Form1 : Form
    {
        // ---- Konfigürasyon ----
        private const int BoardNum = 0;
        private const int LowChan = 0;
        private const int HighChan = 1;
        private const MccDaq.Range DaqRange = MccDaq.Range.Bip10Volts;

        // Rate = channel başına (USB-1608FS)
        private const int SampleRate = 1000;
        private const int DurationSec = 10;

        // TOTAL points (tüm kanallar dahil)
        private int _totalBufferSize;

        // ---- DAQ Değişkenleri ----
        private IntPtr _memHandle = IntPtr.Zero;
        private int _lastScanIndex = 0; // TOTAL point index
        private bool _isAcquiring = false;
        private System.Windows.Forms.Timer _plotTimer;
        private int _sampleRateProxy = SampleRate;

        // ---- Veri Yapıları ----
        private List<double> _timeLog = new List<double>();
        private List<double> _forceLog = new List<double>();
        private List<double> _angleLog = new List<double>();

        private List<PointF> _forceData = new List<PointF>();
        private List<PointF> _angleData = new List<PointF>();

        // ---- Kalibrasyon ve Filtre ----
        private double _forceSlope = 10.0;
        private double _forceOffset = 0.0;
        private double _angleSlope = 72.0;
        private double _angleOffset = 0.0;

        private ButterworthFilter? _forceFilter;

        private int NumChans => (HighChan - LowChan + 1);

        public Form1()
        {
            InitializeComponent();
            LoadCalibration();

            btnStop.Enabled = false;
            btnSaveCsv.Enabled = false;

            _plotTimer = new System.Windows.Forms.Timer();
            _plotTimer.Interval = 50;
            _plotTimer.Tick += OnTimerTick;

            numTargetForce.ValueChanged += (s, e) => canvas.Invalidate();
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

                // Temizlik
                _forceData.Clear();
                _angleData.Clear();
                _timeLog.Clear();
                _forceLog.Clear();
                _angleLog.Clear();
                _lastScanIndex = 0;

                // Buffer boyutu: TOTAL points = rate * süre * kanal_sayısı
                _totalBufferSize = SampleRate * DurationSec * NumChans;

                if (_memHandle != IntPtr.Zero)
                    MccService.WinBufFreeEx(_memHandle);

                _memHandle = MccService.WinBufAllocEx(_totalBufferSize);
                if (_memHandle == IntPtr.Zero)
                    throw new Exception("Memory Alloc Failed");

                var board = new MccBoard(BoardNum);

                // BlockIO .NET'te yok -> Background + ConvertData ile ilerliyoruz
                ScanOptions options = ScanOptions.Background | ScanOptions.ConvertData;

                _sampleRateProxy = SampleRate; // sürücü gerekirse günceller

                // !!! Kritik düzeltme:
                // Count = TOTAL points (tüm kanallar dahil) olmalı.
                int count = _totalBufferSize;

                ErrorInfo err = board.AInScan(
                    LowChan,
                    HighChan,
                    count,
                    ref _sampleRateProxy,
                    DaqRange,
                    _memHandle,
                    options);

                if (err.Value != ErrorInfo.ErrorCode.NoErrors)
                    throw new Exception("AInScan Error: " + err.Message);

                // Filtreyi gerçek örnekleme hızıyla kur
                _forceFilter = new ButterworthFilter(_sampleRateProxy, 15.0);

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

            // Her tick'te, curIndex'e kadar olan veriyi güvenli şekilde oku
            ReadAvailablePoints(curIndex);

            // Scan bittiyse: kalanlar flush edildi, şimdi durdur.
            if (status != MccBoard.Running)
            {
                StopAcquisition();
                lblStatus.Text = "Done.";
                return;
            }

            canvas.Invalidate();
        }

        /// <summary>
        /// WinBuf içerisinden _lastScanIndex ile curIndex arasındaki TOTAL point'leri okur.
        /// Kanal hizasını bozmasın diye okuma miktarını kanal sayısının katına indirir.
        /// Buffer sarma (wrap-around) durumunu yönetir.
        /// </summary>
        private void ReadAvailablePoints(int curIndex)
        {
            if (_memHandle == IntPtr.Zero) return;

            int numChans = NumChans;
            if (numChans <= 0) return;

            if (curIndex == _lastScanIndex) return;

            // curIndex > last: normal okuma
            if (curIndex > _lastScanIndex)
            {
                int pointsToRead = curIndex - _lastScanIndex;
                pointsToRead -= (pointsToRead % numChans); // hizala

                if (pointsToRead <= 0) return;

                ReadAndProcess(_lastScanIndex, pointsToRead);
                _lastScanIndex += pointsToRead;
                return;
            }

            // curIndex < last => wrap-around
            // 1) last -> end
            int tailPoints = _totalBufferSize - _lastScanIndex;
            tailPoints -= (tailPoints % numChans);
            if (tailPoints > 0)
            {
                ReadAndProcess(_lastScanIndex, tailPoints);
                _lastScanIndex = (_lastScanIndex + tailPoints) % _totalBufferSize;
            }

            // 2) 0 -> curIndex
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

            // chunk: TOTAL points; bir "scan" = numChans point
            int scanCount = chunk.Length / numChans;
            if (scanCount <= 0) return;

            var board = new MccBoard(BoardNum);
            int startIndex = _timeLog.Count;

            for (int i = 0; i < scanCount; i++)
            {
                // Kanal sırası interleaved: LowChan..HighChan
                ushort rawForce = chunk[i * numChans + 0];
                ushort rawAngle = chunk[i * numChans + 1];

                float vForce, vAngle;
                board.ToEngUnits(DaqRange, rawForce, out vForce);
                board.ToEngUnits(DaqRange, rawAngle, out vAngle);

                double rawForceKgf = (_forceSlope * vForce) + _forceOffset;
                double angleDeg = (_angleSlope * vAngle) + _angleOffset;

                double filteredForce = rawForceKgf;
                if (chkFilter.Checked && _forceFilter != null)
                    filteredForce = _forceFilter.Filter(rawForceKgf);

                float t = (float)(startIndex + i) / (float)_sampleRateProxy;

                _timeLog.Add(t);
                _forceLog.Add(filteredForce);
                _angleLog.Add(angleDeg);

                _forceData.Add(new PointF(t, (float)filteredForce));
                _angleData.Add(new PointF(t, (float)angleDeg));
            }

            if (_forceLog.Count > 0)
            {
                txtForceVal.Text = _forceLog.Last().ToString("F2");
                txtAngleVal.Text = _angleLog.Last().ToString("F1");
            }
        }

        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float w = canvas.Width;
            float h = canvas.Height;

            // Izgara Çizimi
            using (Pen pGrid = new Pen(Color.LightGray, 1))
            {
                for (int i = 0; i <= 10; i++)
                {
                    float y = i * (h / 10.0f);
                    g.DrawLine(pGrid, 0, y, w, y);
                    float x = i * (w / 10.0f);
                    g.DrawLine(pGrid, x, 0, x, h);
                }
            }

            // Target Zone
            float maxForceScale = 50.0f;
            float targetForce = (float)numTargetForce.Value;
            float tolerance = 2.0f;

            float yZoneTop = h - (((targetForce + tolerance) / maxForceScale) * h);
            float yZoneBottom = h - (((targetForce - tolerance) / maxForceScale) * h);

            using (SolidBrush bZone = new SolidBrush(Color.FromArgb(50, 128, 128, 128)))
            {
                g.FillRectangle(bZone, 0, yZoneTop, w, yZoneBottom - yZoneTop);
            }

            if (_forceData.Count < 2) return;

            // Ekran bozulmasın diye aynı mantık korunuyor
            float maxTime = DurationSec;
            float maxAngleScale = 120.0f;

            using (Pen pForce = new Pen(Color.Red, 2))
            using (Pen pAngle = new Pen(Color.Blue, 2))
            {
                for (int i = 1; i < _forceData.Count; i++)
                {
                    float x1 = (_forceData[i - 1].X / maxTime) * w;
                    float yF1 = h - ((_forceData[i - 1].Y / maxForceScale) * h);
                    float yA1 = h - ((_angleData[i - 1].Y / maxAngleScale) * h);

                    float x2 = (_forceData[i].X / maxTime) * w;
                    float yF2 = h - ((_forceData[i].Y / maxForceScale) * h);
                    float yA2 = h - ((_angleData[i].Y / maxAngleScale) * h);

                    if (x2 <= w)
                    {
                        g.DrawLine(pForce, x1, yF1, x2, yF2);
                        g.DrawLine(pAngle, x1, yA1, x2, yA2);
                    }
                }
            }
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
                    SamplingRate = _sampleRateProxy,
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

        private void LoadCalibration()
        {
            // Çalışma dizini değişirse diye exe yanını da dene
            string p1 = Path.Combine(AppContext.BaseDirectory, "calibration.json");
            string p2 = "calibration.json";
            string? path = File.Exists(p1) ? p1 : (File.Exists(p2) ? p2 : null);

            if (path == null) return;

            try
            {
                var config = JsonSerializer.Deserialize<CalibrationConfig>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config != null)
                {
                    _forceSlope = config.Force.Slope; _forceOffset = config.Force.Offset;
                    _angleSlope = config.Angle.Slope; _angleOffset = config.Angle.Offset;
                    numTargetForce.Value = config.DefaultTargetForce;
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopAcquisition();
            if (_memHandle != IntPtr.Zero) MccService.WinBufFreeEx(_memHandle);
            base.OnFormClosing(e);
        }
    }

    // --- YARDIMCI SINIFLAR (Namespace içinde, Form dışında) ---

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
    }

    public class ManipulationRecord
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public int SamplingRate { get; set; }
        public List<double> Time { get; set; } = new List<double>();
        public List<double> Force { get; set; } = new List<double>();
        public List<double> Angle { get; set; } = new List<double>();
    }
}
