using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MccDaq;
using DaqRange = MccDaq.Range;

namespace CervicalCalibrationTool;

public sealed class Form1 : Form
{
    private const int BoardNum = 0;
    private const DaqRange InputRange = DaqRange.Bip10Volts;
    private const int RequestedSampleRate = 1000;
    private const int CircularBufferSeconds = 10;
    private const int Usb1608FsPacketSize = 31;
    private const int DisplayRate = 250;

    private static readonly SensorDefinition ForceSensor = new("Force", 0, "kgf");
    private static readonly SensorDefinition AngleSensor = new("Angle", 1, "deg");

    private readonly List<SampleRecord> _records = new();
    private readonly List<SampleRecord> _displayRecords = new();
    private readonly BindingList<CalibrationPoint> _calibrationPoints = new();
    private readonly System.Windows.Forms.Timer _readTimer = new() { Interval = 50 };

    private IntPtr _memHandle = IntPtr.Zero;
    private int _totalBufferSize;
    private int _lastBufferIndex;
    private int _actualSampleRate = RequestedSampleRate;
    private long _nextSampleIndex;
    private int _displayDecimation = 4;
    private bool _isAcquiring;
    private int _measurementWindowSamples;
    private int _filterWarmupSamples;
    private int _minimumMeasurementSamples;
    private double _measurementWindowSeconds;
    private double _filterWarmupSeconds;
    private double _minimumMeasurementSeconds;
    private double _measurementKnownValue;
    private int _activeChannel;
    private bool _suppressChannelChange;
    private int _previousChannelIndex;

    private SensorFilterPipeline? _pipeline;

    private ComboBox _cmbChannel = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnClearSession = null!;
    private Button _btnSaveRaw = null!;
    private Label _lblStatus = null!;
    private Label _lblSampling = null!;
    private Label _lblMinimumAcquisition = null!;

    private Label _lblLiveSensor = null!;
    private TextBox _txtRaw = null!;
    private TextBox _txtFiltered = null!;

    private GroupBox _grpFilters = null!;
    private CheckBox _chkNotch = null!;
    private NumericUpDown _numNotchFrequency = null!;
    private NumericUpDown _numNotchQ = null!;
    private CheckBox _chkArtifact = null!;
    private NumericUpDown _numHampelWindow = null!;
    private NumericUpDown _numHampelSigma = null!;
    private NumericUpDown _numMinArtifactMv = null!;
    private CheckBox _chkLowPass = null!;
    private NumericUpDown _numLowPass = null!;

    private GroupBox _grpCalibration = null!;
    private NumericUpDown _numKnownValue = null!;
    private Label _lblKnownUnit = null!;
    private NumericUpDown _numStableWindow = null!;
    private NumericUpDown _numMaxStdMv = null!;
    private NumericUpDown _numMaxDriftMvPerSec = null!;
    private Button _btnCapturePoint = null!;
    private Button _btnRemovePoint = null!;
    private Button _btnExport = null!;

    private PictureBox _canvas = null!;
    private DataGridView _grid = null!;

    public Form1()
    {
        InitializeUi();
        ConfigureGrid();
        HookMeasurementSettingEvents();

        _readTimer.Tick += ReadTimer_Tick;
        FormClosing += Form1_FormClosing;

        _previousChannelIndex = _cmbChannel.SelectedIndex;
        UpdateChannelUi();
        UpdateMinimumAcquisitionUi();
        UpdateAcquisitionUi();
    }

    private void InitializeUi()
    {
        Text = "Single-Channel Calibration Data Collector — Raw Voltage + Filtering";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(1280, 760);
        ClientSize = new Size(1440, 860);
        Font = new Font("Segoe UI", 9F);

        SuspendLayout();

        // Do not assign SplitterDistance/PanelMinSize inside the initializer.
        // At that moment the control still has its small design-time default size,
        // and strict minimum sizes can raise an ArgumentOutOfRangeException before
        // the form becomes visible. The final values are applied after first layout.
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1
        };
        Controls.Add(mainSplit);

        var left = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        mainSplit.Panel1.Controls.Add(left);

        int y = 10;
        GroupBox acquisition = BuildAcquisitionGroup(y);
        left.Controls.Add(acquisition);
        y += acquisition.Height + 10;

        GroupBox live = BuildLiveGroup(y);
        left.Controls.Add(live);
        y += live.Height + 10;

        _grpCalibration = BuildCalibrationGroup(y);
        left.Controls.Add(_grpCalibration);
        y += _grpCalibration.Height + 10;

        _grpFilters = BuildFilterGroup(y);
        left.Controls.Add(_grpFilters);
        y += _grpFilters.Height + 10;


        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6
        };
        mainSplit.Panel2.Controls.Add(rightSplit);

        _canvas = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _canvas.Paint += Canvas_Paint;
        rightSplit.Panel1.Controls.Add(_canvas);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            RowHeadersVisible = false
        };
        rightSplit.Panel2.Controls.Add(_grid);

        ResumeLayout(performLayout: true);

        // Apply the requested layout only after Windows Forms has calculated the
        // real DPI-scaled client sizes. BeginInvoke also avoids running before the
        // first dock/layout pass has completed.
        Shown += (_, _) => BeginInvoke(new Action(() =>
        {
            ApplyInitialSplitterLayout(mainSplit, rightSplit);
        }));
    }

    private static void ApplyInitialSplitterLayout(
        SplitContainer mainSplit,
        SplitContainer rightSplit)
    {
        SetSplitterLayoutSafe(
            mainSplit,
            desiredDistance: 475,
            desiredPanel1Minimum: 430,
            desiredPanel2Minimum: 500);

        SetSplitterLayoutSafe(
            rightSplit,
            desiredDistance: 395,
            desiredPanel1Minimum: 260,
            desiredPanel2Minimum: 220);
    }

    private static void SetSplitterLayoutSafe(
        SplitContainer split,
        int desiredDistance,
        int desiredPanel1Minimum,
        int desiredPanel2Minimum)
    {
        int totalLength = split.Orientation == Orientation.Vertical
            ? split.ClientSize.Width
            : split.ClientSize.Height;

        if (totalLength <= split.SplitterWidth + 2)
            return;

        // First set a safe distance while both panel minimums are still zero.
        int minimumDistance = 1;
        int maximumDistance = Math.Max(
            minimumDistance,
            totalLength - split.SplitterWidth - 1);
        int distance = Math.Clamp(
            desiredDistance,
            minimumDistance,
            maximumDistance);
        split.SplitterDistance = distance;

        // Minimums are adapted to the actual monitor/DPI size instead of assuming
        // that the requested 1440 x 860 client area is always available.
        int panel1Available = Math.Max(0, distance);
        int panel2Available = Math.Max(
            0,
            totalLength - distance - split.SplitterWidth);

        split.Panel1MinSize = Math.Min(
            desiredPanel1Minimum,
            panel1Available);
        split.Panel2MinSize = Math.Min(
            desiredPanel2Minimum,
            panel2Available);
    }

    private GroupBox BuildAcquisitionGroup(int top)
    {
        var group = NewGroup("1. Single-Channel Acquisition", top, 278);

        group.Controls.Add(NewLabel("Calibration channel", 15, 29, 135));
        _cmbChannel = new ComboBox
        {
            Location = new Point(155, 26),
            Size = new Size(245, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbChannel.Items.AddRange(new object[]
        {
            "Force — Channel 0",
            "Angle — Channel 1"
        });
        _cmbChannel.SelectedIndex = 0;
        _cmbChannel.SelectedIndexChanged += ChannelSelectionChanged;
        group.Controls.Add(_cmbChannel);

        _btnStart = NewButton("START", 15, 63, 195, 34);
        _btnStart.BackColor = Color.LightGreen;
        _btnStart.Click += BtnStart_Click;
        group.Controls.Add(_btnStart);

        _btnStop = NewButton("STOP", 220, 63, 195, 34);
        _btnStop.BackColor = Color.LightCoral;
        _btnStop.Click += (_, _) => StopAndCaptureMeasurement();
        group.Controls.Add(_btnStop);

        _btnSaveRaw = NewButton("SAVE CURRENT RAW (.CSV)", 15, 105, 265, 32);
        _btnSaveRaw.Click += BtnSaveRaw_Click;
        group.Controls.Add(_btnSaveRaw);

        _btnClearSession = NewButton("CLEAR SESSION", 290, 105, 125, 32);
        _btnClearSession.Click += (_, _) => ClearSession();
        group.Controls.Add(_btnClearSession);

        _lblStatus = new Label
        {
            Location = new Point(15, 147),
            Size = new Size(400, 23),
            Text = "Ready."
        };
        group.Controls.Add(_lblStatus);

        _lblSampling = new Label
        {
            Location = new Point(15, 172),
            Size = new Size(400, 42)
        };
        group.Controls.Add(_lblSampling);

        _lblMinimumAcquisition = new Label
        {
            Location = new Point(15, 216),
            Size = new Size(400, 50),
            ForeColor = Color.DarkBlue,
            Text = "Minimum acquisition time will be calculated automatically."
        };
        group.Controls.Add(_lblMinimumAcquisition);
        return group;
    }

    private GroupBox BuildLiveGroup(int top)
    {
        var group = NewGroup("2. Live Voltage", top, 115);
        group.Controls.Add(NewLabel("Channel", 15, 28, 110));
        group.Controls.Add(NewLabel("Raw voltage", 130, 28, 120));
        group.Controls.Add(NewLabel("Filtered", 275, 28, 125));

        _lblLiveSensor = NewLabel("Force Ch0", 15, 63, 110);
        _txtRaw = NewReadOnlyText(130, 58, 130);
        _txtFiltered = NewReadOnlyText(275, 58, 140);
        group.Controls.Add(_lblLiveSensor);
        group.Controls.Add(_txtRaw);
        group.Controls.Add(_txtFiltered);
        return group;
    }

    private GroupBox BuildFilterGroup(int top)
    {
        var group = NewGroup("4. Filters", top, 300);

        _chkNotch = NewCheckBox("50 Hz notch filter", 15, 28, true);
        group.Controls.Add(_chkNotch);
        group.Controls.Add(NewLabel("Frequency (Hz)", 35, 59, 110));
        _numNotchFrequency = NewNumeric(155, 55, 90, 1, 200, 50, 1);
        group.Controls.Add(_numNotchFrequency);
        group.Controls.Add(NewLabel("Q", 260, 59, 25));
        _numNotchQ = NewNumeric(300, 55, 115, 1, 100, 30, 1);
        group.Controls.Add(_numNotchQ);

        _chkArtifact = NewCheckBox("Suppress motion spikes (Hampel)", 15, 95, true);
        group.Controls.Add(_chkArtifact);
        group.Controls.Add(NewLabel("Window (samples)", 35, 126, 120));
        _numHampelWindow = NewNumeric(155, 122, 90, 5, 1001, 101, 0, increment: 2);
        group.Controls.Add(_numHampelWindow);
        group.Controls.Add(NewLabel("Threshold (σ)", 260, 126, 65));
        _numHampelSigma = NewNumeric(335, 122, 80, 1, 10, 3.5m, 1, increment: 0.1m);
        group.Controls.Add(_numHampelSigma);

        group.Controls.Add(NewLabel("Minimum spike threshold (mV)", 35, 161, 200));
        _numMinArtifactMv = NewNumeric(250, 157, 165, 0, 1000, 2, 1, increment: 0.5m);
        group.Controls.Add(_numMinArtifactMv);

        _chkLowPass = NewCheckBox("4th-order low-pass filter", 15, 197, true);
        group.Controls.Add(_chkLowPass);
        group.Controls.Add(NewLabel("Cutoff frequency (Hz)", 35, 228, 150));
        _numLowPass = NewNumeric(190, 224, 100, 0.5m, 200, 10, 1, increment: 0.5m);
        group.Controls.Add(_numLowPass);

        var note = new Label
        {
            Location = new Point(15, 260),
            Size = new Size(400, 32),
            ForeColor = Color.DimGray,
            Text = "No high-pass filter is used; DC/offset information is preserved."
        };
        group.Controls.Add(note);
        return group;
    }

    private GroupBox BuildCalibrationGroup(int top)
    {
        var group = NewGroup("3. Measurement Point", top, 265);

        group.Controls.Add(NewLabel("Known value", 15, 30, 110));
        _numKnownValue = NewNumeric(125, 26, 205, -10000, 10000, 0, 3, increment: 0.1m);
        group.Controls.Add(_numKnownValue);
        _lblKnownUnit = NewLabel("kgf", 340, 30, 65);
        group.Controls.Add(_lblKnownUnit);

        group.Controls.Add(NewLabel("Stable window (s)", 15, 67, 145));
        _numStableWindow = NewNumeric(175, 63, 95, 0.25m, 20, 5, 2, increment: 0.25m);
        group.Controls.Add(_numStableWindow);

        group.Controls.Add(NewLabel("Maximum SD (mV)", 15, 102, 145));
        _numMaxStdMv = NewNumeric(175, 98, 95, 0.1m, 500, 2, 1, increment: 0.5m);
        group.Controls.Add(_numMaxStdMv);

        group.Controls.Add(NewLabel("Maximum drift (mV/s)", 15, 137, 160));
        _numMaxDriftMvPerSec = NewNumeric(175, 133, 95, 0.1m, 1000, 1, 1, increment: 0.5m);
        group.Controls.Add(_numMaxDriftMvPerSec);

        _btnCapturePoint = NewButton("APPLY LOAD BEFORE START — STOP SAVES THE POINT", 15, 174, 400, 34);
        _btnCapturePoint.BackColor = Color.LightSteelBlue;
        _btnCapturePoint.Enabled = false;
        group.Controls.Add(_btnCapturePoint);

        _btnRemovePoint = NewButton("REMOVE SELECTED POINT", 15, 216, 195, 31);
        _btnRemovePoint.Click += BtnRemovePoint_Click;
        group.Controls.Add(_btnRemovePoint);

        _btnExport = NewButton("EXPORT SAVED POINTS (.CSV)", 220, 216, 195, 31);
        _btnExport.Click += BtnExport_Click;
        group.Controls.Add(_btnExport);
        return group;
    }

    private void ConfigureGrid()
    {
        _grid.DataSource = _calibrationPoints;
        _grid.Columns.Add(NewTextColumn("Known", nameof(CalibrationPoint.KnownValue), 75, "0.###"));
        _grid.Columns.Add(NewTextColumn("Unit", nameof(CalibrationPoint.Unit), 55));
        _grid.Columns.Add(NewTextColumn("Mean raw V", nameof(CalibrationPoint.MeanRawVoltage), 100, "0.000000"));
        _grid.Columns.Add(NewTextColumn("Mean filtered V", nameof(CalibrationPoint.MeanFilteredVoltage), 110, "0.000000"));
        _grid.Columns.Add(NewTextColumn("Filtered SD (mV)", nameof(CalibrationPoint.StandardDeviationMillivolts), 105, "0.000"));
        _grid.Columns.Add(NewTextColumn("Filtered P-P (mV)", nameof(CalibrationPoint.PeakToPeakMillivolts), 110, "0.000"));
        _grid.Columns.Add(NewTextColumn("Filtered drift (mV/s)", nameof(CalibrationPoint.DriftMillivoltsPerSecond), 125, "0.000"));
        _grid.Columns.Add(NewTextColumn("Artifact %", nameof(CalibrationPoint.ArtifactPercent), 80, "0.0"));
        _grid.Columns.Add(NewTextColumn("N", nameof(CalibrationPoint.SampleCount), 60));
        _grid.Columns.Add(NewTextColumn("Time", nameof(CalibrationPoint.CapturedAt), 130, "HH:mm:ss"));
    }

    private void HookMeasurementSettingEvents()
    {
        EventHandler handler = (_, _) =>
        {
            if (!_isAcquiring)
                UpdateMinimumAcquisitionUi();
        };

        _numStableWindow.ValueChanged += handler;
        _chkNotch.CheckedChanged += handler;
        _numNotchFrequency.ValueChanged += handler;
        _numNotchQ.ValueChanged += handler;
        _chkArtifact.CheckedChanged += handler;
        _numHampelWindow.ValueChanged += handler;
        _chkLowPass.CheckedChanged += handler;
        _numLowPass.ValueChanged += handler;
    }

    private void ConfigureMeasurementTiming()
    {
        FilterSettings settings = ReadFilterSettings(_actualSampleRate);
        _measurementWindowSeconds = (double)_numStableWindow.Value;
        _measurementWindowSamples = Math.Max(
            5,
            (int)Math.Ceiling(_measurementWindowSeconds * _actualSampleRate));

        _filterWarmupSeconds = CalculateAutomaticWarmupSeconds(settings);
        _filterWarmupSamples = Math.Max(
            1,
            (int)Math.Ceiling(_filterWarmupSeconds * _actualSampleRate));

        _minimumMeasurementSamples = checked(_measurementWindowSamples + _filterWarmupSamples);
        _minimumMeasurementSeconds = _minimumMeasurementSamples / (double)_actualSampleRate;
        _measurementKnownValue = (double)_numKnownValue.Value;
    }

    private static double CalculateAutomaticWarmupSeconds(FilterSettings settings)
    {
        // One second is a conservative minimum for classroom measurements and
        // allows the DAQ stream and all filter states to settle before the final
        // stable analysis window begins.
        double warmupSeconds = 1.0;

        if (settings.EnableNotch)
        {
            // Approximate five time constants for a narrow-band biquad notch.
            double notchSettling = 5.0 * settings.NotchQ
                                 / (Math.PI * settings.NotchFrequency);
            warmupSeconds = Math.Max(warmupSeconds, notchSettling);
        }

        if (settings.EnableArtifactFilter)
        {
            double hampelFill = settings.HampelWindow / settings.SampleRate;
            warmupSeconds = Math.Max(warmupSeconds, hampelFill);
        }

        if (settings.EnableLowPass)
        {
            // Conservative settling estimate for two cascaded second-order
            // Butterworth sections (fourth-order low-pass).
            double lowPassSettling = 20.0
                                   / (2.0 * Math.PI * settings.LowPassCutoff);
            warmupSeconds = Math.Max(warmupSeconds, lowPassSettling);
        }

        return warmupSeconds;
    }

    private void UpdateMinimumAcquisitionUi()
    {
        if (_lblMinimumAcquisition is null)
            return;

        int rate = Math.Max(1, _actualSampleRate);
        FilterSettings settings = ReadFilterSettings(rate);
        double windowSeconds = (double)_numStableWindow.Value;
        double warmupSeconds = CalculateAutomaticWarmupSeconds(settings);
        int windowSamples = Math.Max(5, (int)Math.Ceiling(windowSeconds * rate));
        int warmupSamples = Math.Max(1, (int)Math.Ceiling(warmupSeconds * rate));
        double minimumSeconds = (windowSamples + warmupSamples) / (double)rate;

        _lblMinimumAcquisition.Text =
            $"Automatic minimum: {minimumSeconds:F2} s " +
            $"({warmupSeconds:F2} s settling + {windowSeconds:F2} s stable window).\r\n" +
            "STOP saves one point and clears the temporary raw buffer.";
    }

    private void ChannelSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressChannelChange)
            return;

        if ((_records.Count > 0 || _calibrationPoints.Count > 0) && _cmbChannel.SelectedIndex != _previousChannelIndex)
        {
            _suppressChannelChange = true;
            _cmbChannel.SelectedIndex = _previousChannelIndex;
            _suppressChannelChange = false;
            MessageBox.Show(
                this,
                "This is a single-channel calibration session. Clear the current session before changing channels.",
                "Channel locked",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _previousChannelIndex = _cmbChannel.SelectedIndex;
        UpdateChannelUi();
    }

    private void UpdateChannelUi()
    {
        SensorDefinition sensor = SelectedSensor;
        if (_lblLiveSensor is not null)
            _lblLiveSensor.Text = $"{sensor.Name} Ch{sensor.Channel}";
        if (_lblKnownUnit is not null)
            _lblKnownUnit.Text = sensor.Unit;
        if (_canvas is not null)
            _canvas.Invalidate();
        UpdateAcquisitionUi();
    }

    private void BtnStart_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_isAcquiring)
                return;

            ClearAcquisitionData();
            _lastBufferIndex = 0;
            _nextSampleIndex = 0;
            _actualSampleRate = RequestedSampleRate;
            _activeChannel = SelectedSensor.Channel;
            int requestedBufferPoints = RequestedSampleRate * CircularBufferSeconds;
            _totalBufferSize = AlignUpToMultiple(requestedBufferPoints, Usb1608FsPacketSize);

            FreeBuffer();
            _memHandle = MccService.WinBufAllocEx(_totalBufferSize);
            if (_memHandle == IntPtr.Zero)
                throw new InvalidOperationException("DAQ memory allocation failed.");

            var board = new MccBoard(BoardNum);
            int count = _totalBufferSize;
            ScanOptions options = ScanOptions.Background
                                | ScanOptions.Continuous
                                | ScanOptions.ConvertData;

            ErrorInfo error = board.AInScan(
                _activeChannel,
                _activeChannel,
                count,
                ref _actualSampleRate,
                InputRange,
                _memHandle,
                options);

            if (error.Value != ErrorInfo.ErrorCode.NoErrors)
                throw new InvalidOperationException("AInScan error: " + error.Message);

            _pipeline = new SensorFilterPipeline(ReadFilterSettings(_actualSampleRate));
            _displayDecimation = Math.Max(1, (int)Math.Round((double)_actualSampleRate / DisplayRate));
            ConfigureMeasurementTiming();

            _isAcquiring = true;
            _readTimer.Start();
            _lblStatus.Text =
                $"Acquiring {SelectedSensor.Name} — wait {_minimumMeasurementSeconds:F2} s before STOP is enabled.";
            UpdateMinimumAcquisitionUi();
            UpdateAcquisitionUi();
        }
        catch (Exception ex)
        {
            StopAcquisition();
            MessageBox.Show(this, ex.Message, "Start error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReadTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isAcquiring || _memHandle == IntPtr.Zero)
            return;

        try
        {
            var board = new MccBoard(BoardNum);
            board.GetStatus(out short status, out int currentCount, out int currentIndex, FunctionType.AiFunction);
            ReadAvailablePoints(currentIndex, currentCount);

            if (status != MccBoard.Running)
            {
                StopAcquisition();
                _lblStatus.Text = "DAQ background scan ended.";
            }

            _canvas.Invalidate();
        }
        catch (Exception ex)
        {
            StopAcquisition();
            MessageBox.Show(this, ex.Message, "Read error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReadAvailablePoints(int currentIndex, int currentCount)
    {
        if (_memHandle == IntPtr.Zero || currentIndex < 0 || currentCount <= 0)
            return;

        // MCC reports the index of the most recently completed sample. Convert
        // it to the exclusive write position so the newest sample is included.
        int writeExclusive = (currentIndex + 1) % _totalBufferSize;
        if (writeExclusive == _lastBufferIndex)
            return;

        if (writeExclusive > _lastBufferIndex)
        {
            int pointsToRead = writeExclusive - _lastBufferIndex;
            ReadAndProcess(_lastBufferIndex, pointsToRead);
            _lastBufferIndex = writeExclusive;
            return;
        }

        int tailPoints = _totalBufferSize - _lastBufferIndex;
        if (tailPoints > 0)
            ReadAndProcess(_lastBufferIndex, tailPoints);

        if (writeExclusive > 0)
            ReadAndProcess(0, writeExclusive);

        _lastBufferIndex = writeExclusive;
    }

    private static int AlignUpToMultiple(int value, int multiple)
    {
        if (multiple <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiple));

        int remainder = value % multiple;
        return remainder == 0 ? value : checked(value + multiple - remainder);
    }

    private void ReadAndProcess(int startPoint, int pointsToRead)
    {
        if (pointsToRead <= 0 || _memHandle == IntPtr.Zero)
            return;

        ushort[] chunk = new ushort[pointsToRead];
        MccService.WinBufToArray(_memHandle, chunk, startPoint, pointsToRead);
        ProcessChunk(chunk);
    }

    private void ProcessChunk(ushort[] chunk)
    {
        if (_pipeline is null || chunk.Length == 0)
            return;

        var board = new MccBoard(BoardNum);
        SampleRecord? lastRecord = null;

        foreach (ushort adc in chunk)
        {
            board.ToEngUnits(InputRange, adc, out float rawVoltage);
            FilterOutput output = _pipeline.Process(rawVoltage);

            long sampleIndex = _nextSampleIndex++;
            var record = new SampleRecord
            {
                SampleIndex = sampleIndex,
                TimeSeconds = sampleIndex / (double)_actualSampleRate,
                Adc = adc,
                RawVoltage = output.RawVoltage,
                NotchVoltage = output.NotchVoltage,
                FilteredVoltage = output.FilteredVoltage,
                Artifact = output.ArtifactDetected
            };

            _records.Add(record);
            if (sampleIndex % _displayDecimation == 0)
                _displayRecords.Add(record);
            lastRecord = record;
        }

        TrimDisplayBuffer();

        if (lastRecord is not null)
        {
            _txtRaw.Text = lastRecord.RawVoltage.ToString("F6", CultureInfo.InvariantCulture);
            _txtFiltered.Text = lastRecord.FilteredVoltage.ToString("F6", CultureInfo.InvariantCulture);

            if (_records.Count < _minimumMeasurementSamples)
            {
                int remainingSamples = _minimumMeasurementSamples - _records.Count;
                double remainingSeconds = remainingSamples / (double)Math.Max(1, _actualSampleRate);
                _lblStatus.Text =
                    $"Acquiring {SelectedSensor.Name} — {remainingSeconds:F2} s remaining before STOP is enabled.";
            }
            else
            {
                _lblStatus.Text =
                    $"Measurement ready — {_records.Count:N0} samples acquired. Press STOP & SAVE POINT.";
            }

            UpdateAcquisitionUi();
        }
    }

    private void TrimDisplayBuffer()
    {
        int maxDisplayRecords = DisplayRate * 30;
        if (_displayRecords.Count <= maxDisplayRecords)
            return;

        int removeCount = _displayRecords.Count - maxDisplayRecords;
        _displayRecords.RemoveRange(0, removeCount);
    }

    private void StopAndCaptureMeasurement()
    {
        if (!_isAcquiring)
            return;

        if (_records.Count < _minimumMeasurementSamples)
        {
            int remainingSamples = _minimumMeasurementSamples - _records.Count;
            double remainingSeconds = remainingSamples / (double)Math.Max(1, _actualSampleRate);
            MessageBox.Show(
                this,
                $"The automatic minimum acquisition period has not been completed. " +
                $"Wait approximately {remainingSeconds:F2} seconds.",
                "Measurement not ready",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        StopAcquisition(captureMeasurement: true);
    }

    private void StopAcquisition(bool captureMeasurement = false)
    {
        bool wasAcquiring = _isAcquiring;

        if (wasAcquiring && _memHandle != IntPtr.Zero)
        {
            try
            {
                var board = new MccBoard(BoardNum);
                board.GetStatus(out _, out int currentCount, out int currentIndex, FunctionType.AiFunction);
                ReadAvailablePoints(currentIndex, currentCount);
            }
            catch
            {
                // Preserve already acquired samples if the final DAQ read fails.
            }
        }

        _isAcquiring = false;
        _readTimer.Stop();
        try { new MccBoard(BoardNum).StopBackground(FunctionType.AiFunction); } catch { }

        string finalStatus;
        if (captureMeasurement && _records.Count >= _minimumMeasurementSamples)
        {
            bool added = TryCaptureCurrentMeasurement(out string message);
            finalStatus = added
                ? message
                : "Measurement rejected; transient samples were cleared. Apply the load again and repeat.";
        }
        else if (wasAcquiring)
        {
            finalStatus = "Acquisition stopped without saving a calibration point.";
        }
        else
        {
            finalStatus = _lblStatus.Text;
        }

        // Raw samples are temporary for one START/STOP measurement cycle only.
        // The accepted CalibrationPoint remains in the grid; the DAQ buffer,
        // filter state, graph data, and raw sample list are discarded.
        ClearAcquisitionData();
        _pipeline = null;
        FreeBuffer();

        _lblStatus.Text = finalStatus;
        UpdateMinimumAcquisitionUi();
        UpdateAcquisitionUi();
        _canvas.Invalidate();
    }

    private void ClearAcquisitionData()
    {
        _records.Clear();
        _displayRecords.Clear();
        _nextSampleIndex = 0;
        _txtRaw.Clear();
        _txtFiltered.Clear();
        _canvas.Invalidate();
    }

    private void ClearSession()
    {
        if (_isAcquiring)
            return;

        ClearAcquisitionData();
        _calibrationPoints.Clear();
        _lblStatus.Text = "Single-channel session cleared.";
        UpdateAcquisitionUi();
    }

    private void BtnSaveRaw_Click(object? sender, EventArgs e)
    {
        if (_records.Count == 0)
            return;

        SensorDefinition sensor = SelectedSensor;
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file|*.csv",
            FileName = $"{sensor.Name}_Ch{sensor.Channel}_RawVoltage_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SaveRawCsv(dialog.FileName);
            MessageBox.Show(
                this,
                "Single-channel raw ADC counts, raw voltages, and filter outputs were saved.",
                "Save completed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveRawCsv(string path)
    {
        var invariant = CultureInfo.InvariantCulture;
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("sample_index,time_s,adc,raw_v,notch_v,filtered_v,artifact");

        foreach (SampleRecord record in _records)
        {
            writer.Write(record.SampleIndex.ToString(invariant));
            writer.Write(',');
            writer.Write(record.TimeSeconds.ToString("0.000000", invariant));
            writer.Write(',');
            writer.Write(record.Adc.ToString(invariant));
            writer.Write(',');
            writer.Write(record.RawVoltage.ToString("0.000000000", invariant));
            writer.Write(',');
            writer.Write(record.NotchVoltage.ToString("0.000000000", invariant));
            writer.Write(',');
            writer.Write(record.FilteredVoltage.ToString("0.000000000", invariant));
            writer.Write(',');
            writer.WriteLine(record.Artifact ? "1" : "0");
        }
    }

    private bool TryCaptureCurrentMeasurement(out string statusMessage)
    {
        statusMessage = string.Empty;

        if (_records.Count < _minimumMeasurementSamples || _measurementWindowSamples < 5)
        {
            statusMessage = "Insufficient samples; no calibration point was added.";
            return false;
        }

        SampleRecord[] window = _records
            .Skip(_records.Count - _measurementWindowSamples)
            .Take(_measurementWindowSamples)
            .ToArray();

        double[] raw = window.Select(record => record.RawVoltage).ToArray();
        double[] filtered = window.Select(record => record.FilteredVoltage).ToArray();
        bool[] artifacts = window.Select(record => record.Artifact).ToArray();

        double meanRaw = raw.Average();
        double meanFiltered = filtered.Average();

        // Classroom calibration-point acceptance uses the final filtered signal.
        // This restores the proven V5 behavior: the large mains component visible
        // in the raw trace must not cause a stable blue trace to be rejected.
        // Raw and notch-filtered samples remain available in the optional raw CSV.
        double standardDeviation = StandardDeviation(filtered, meanFiltered);
        double peakToPeak = filtered.Max() - filtered.Min();
        double artifactFraction = artifacts.Count(value => value) / (double)artifacts.Length;
        double drift = LinearSlopePerSecond(filtered, _actualSampleRate);

        double maxStd = (double)_numMaxStdMv.Value / 1000.0;
        double maxDrift = (double)_numMaxDriftMvPerSec.Value / 1000.0;

        var failures = new List<string>();
        if (standardDeviation > maxStd)
            failures.Add($"SD = {standardDeviation * 1000.0:F3} mV (limit {maxStd * 1000.0:F3} mV)");
        if (Math.Abs(drift) > maxDrift)
            failures.Add($"drift = {drift * 1000.0:F3} mV/s (limit ±{maxDrift * 1000.0:F3} mV/s)");
        if (artifactFraction > 0.10)
            failures.Add($"artifact fraction = {artifactFraction * 100.0:F1}% (limit 10%)");

        if (failures.Count > 0)
        {
            MessageBox.Show(
                this,
                "The final stable window was rejected; no point was added.\r\n\r\n" +
                string.Join("\r\n", failures),
                "Motion/instability detected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        SensorDefinition sensor = SelectedSensor;
        _calibrationPoints.Add(new CalibrationPoint
        {
            Sensor = sensor.Name,
            Channel = sensor.Channel,
            Unit = sensor.Unit,
            KnownValue = _measurementKnownValue,
            MeanRawVoltage = meanRaw,
            MeanFilteredVoltage = meanFiltered,
            StandardDeviationVolts = standardDeviation,
            PeakToPeakVolts = peakToPeak,
            DriftVoltsPerSecond = drift,
            ArtifactFraction = artifactFraction,
            SampleCount = _measurementWindowSamples,
            CapturedAt = DateTime.Now
        });

        statusMessage =
            $"{sensor.Name} Ch{sensor.Channel} point saved: {_measurementKnownValue:0.###} {sensor.Unit}, " +
            $"N = {_measurementWindowSamples:N0}. Temporary raw buffer cleared.";
        return true;
    }

    private void BtnRemovePoint_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.DataBoundItem is not CalibrationPoint point)
            return;

        _calibrationPoints.Remove(point);
        UpdateAcquisitionUi();
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_calibrationPoints.Count == 0)
            return;

        SensorDefinition sensor = SelectedSensor;
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file|*.csv",
            FileName = $"{sensor.Name}_Ch{sensor.Channel}_MeasurementPoints_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SaveMeasurementPointsCsv(dialog.FileName);
            MessageBox.Show(
                this,
                $"{_calibrationPoints.Count} accepted measurement points were saved as CSV.",
                "Export completed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveMeasurementPointsCsv(string path)
    {
        var invariant = CultureInfo.InvariantCulture;
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("sensor,channel,known_value,unit,mean_raw_v,mean_filtered_v,filtered_sd_mv,filtered_p_p_mv,filtered_drift_mv_s,artifact_percent,n,captured_at");

        foreach (CalibrationPoint point in _calibrationPoints)
        {
            writer.Write(CsvEscape(point.Sensor));
            writer.Write(',');
            writer.Write(point.Channel.ToString(invariant));
            writer.Write(',');
            writer.Write(point.KnownValue.ToString("0.#########", invariant));
            writer.Write(',');
            writer.Write(CsvEscape(point.Unit));
            writer.Write(',');
            writer.Write(point.MeanRawVoltage.ToString("0.000000000", invariant));
            writer.Write(',');
            writer.Write(point.MeanFilteredVoltage.ToString("0.000000000", invariant));
            writer.Write(',');
            writer.Write(point.StandardDeviationMillivolts.ToString("0.000000", invariant));
            writer.Write(',');
            writer.Write(point.PeakToPeakMillivolts.ToString("0.000000", invariant));
            writer.Write(',');
            writer.Write(point.DriftMillivoltsPerSecond.ToString("0.000000", invariant));
            writer.Write(',');
            writer.Write(point.ArtifactPercent.ToString("0.000", invariant));
            writer.Write(',');
            writer.Write(point.SampleCount.ToString(invariant));
            writer.Write(',');
            writer.WriteLine(point.CapturedAt.ToString("O", invariant));
        }
    }

    private static string CsvEscape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.White);

        Rectangle plot = new(70, 30, Math.Max(1, _canvas.Width - 95), Math.Max(1, _canvas.Height - 85));
        DrawGridAndLabels(graphics, plot);

        if (_displayRecords.Count < 2)
        {
            using var brush = new SolidBrush(Color.DimGray);
            graphics.DrawString("Single-channel raw and filtered voltage will be displayed here.", Font, brush, plot.Left + 20, plot.Top + 20);
            return;
        }

        double lastTime = _displayRecords[^1].TimeSeconds;
        double firstTime = Math.Max(_displayRecords[0].TimeSeconds, lastTime - 10.0);
        SampleRecord[] visible = _displayRecords
            .Where(record => record.TimeSeconds >= firstTime)
            .ToArray();
        if (visible.Length < 2)
            return;

        double[] raw = visible.Select(record => record.RawVoltage).ToArray();
        double[] filtered = visible.Select(record => record.FilteredVoltage).ToArray();

        double minVoltage = Math.Min(raw.Min(), filtered.Min());
        double maxVoltage = Math.Max(raw.Max(), filtered.Max());
        double span = maxVoltage - minVoltage;
        if (span < 0.002)
        {
            minVoltage -= 0.001;
            maxVoltage += 0.001;
        }
        else
        {
            minVoltage -= span * 0.08;
            maxVoltage += span * 0.08;
        }

        DrawSeries(graphics, plot, visible, raw, firstTime, lastTime, minVoltage, maxVoltage, Color.Gray, 1.0f);
        DrawSeries(graphics, plot, visible, filtered, firstTime, lastTime, minVoltage, maxVoltage, Color.RoyalBlue, 2.0f);
        DrawArtifactMarkers(graphics, plot, visible, firstTime, lastTime, minVoltage, maxVoltage);

        using var textBrush = new SolidBrush(Color.Black);
        SensorDefinition sensor = SelectedSensor;
        graphics.DrawString($"{sensor.Name} (Ch{sensor.Channel}) — last 10 s", Font, textBrush, plot.Left, 7);
        graphics.DrawString($"{maxVoltage:F4} V", Font, textBrush, 5, plot.Top - 7);
        graphics.DrawString($"{minVoltage:F4} V", Font, textBrush, 5, plot.Bottom - 7);

        using var rawPen = new Pen(Color.Gray, 2);
        using var filteredPen = new Pen(Color.RoyalBlue, 2);
        graphics.DrawLine(rawPen, plot.Right - 185, 14, plot.Right - 155, 14);
        graphics.DrawString("Raw", Font, textBrush, plot.Right - 150, 6);
        graphics.DrawLine(filteredPen, plot.Right - 95, 14, plot.Right - 65, 14);
        graphics.DrawString("Filtered", Font, textBrush, plot.Right - 60, 6);
    }

    private static void DrawGridAndLabels(Graphics graphics, Rectangle plot)
    {
        using var gridPen = new Pen(Color.Gainsboro, 1);
        using var axisPen = new Pen(Color.DimGray, 1.5f);
        for (int i = 0; i <= 10; i++)
        {
            float x = plot.Left + (i * plot.Width / 10f);
            graphics.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
        }
        for (int i = 0; i <= 8; i++)
        {
            float y = plot.Top + (i * plot.Height / 8f);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
        }
        graphics.DrawRectangle(axisPen, plot);
    }

    private static void DrawSeries(
        Graphics graphics,
        Rectangle plot,
        SampleRecord[] records,
        double[] values,
        double firstTime,
        double lastTime,
        double minVoltage,
        double maxVoltage,
        Color color,
        float width)
    {
        double timeSpan = Math.Max(1e-9, lastTime - firstTime);
        double voltageSpan = Math.Max(1e-12, maxVoltage - minVoltage);
        int step = Math.Max(1, records.Length / Math.Max(1, plot.Width));
        var points = new List<PointF>((records.Length / step) + 2);

        for (int i = 0; i < records.Length; i += step)
        {
            float x = plot.Left + (float)((records[i].TimeSeconds - firstTime) / timeSpan * plot.Width);
            float y = plot.Bottom - (float)((values[i] - minVoltage) / voltageSpan * plot.Height);
            points.Add(new PointF(x, y));
        }

        if (points.Count >= 2)
        {
            using var pen = new Pen(color, width);
            graphics.DrawLines(pen, points.ToArray());
        }
    }

    private static void DrawArtifactMarkers(
        Graphics graphics,
        Rectangle plot,
        SampleRecord[] records,
        double firstTime,
        double lastTime,
        double minVoltage,
        double maxVoltage)
    {
        double timeSpan = Math.Max(1e-9, lastTime - firstTime);
        double voltageSpan = Math.Max(1e-12, maxVoltage - minVoltage);
        using var brush = new SolidBrush(Color.Red);

        foreach (SampleRecord record in records)
        {
            if (!record.Artifact)
                continue;

            float x = plot.Left + (float)((record.TimeSeconds - firstTime) / timeSpan * plot.Width);
            float y = plot.Bottom - (float)((record.RawVoltage - minVoltage) / voltageSpan * plot.Height);
            graphics.FillEllipse(brush, x - 2, y - 2, 4, 4);
        }
    }

    private FilterSettings ReadFilterSettings(double sampleRate)
        => new()
        {
            SampleRate = sampleRate,
            EnableNotch = _chkNotch.Checked,
            NotchFrequency = (double)_numNotchFrequency.Value,
            NotchQ = (double)_numNotchQ.Value,
            EnableArtifactFilter = _chkArtifact.Checked,
            HampelWindow = MakeOdd((int)_numHampelWindow.Value),
            HampelSigma = (double)_numHampelSigma.Value,
            MinimumArtifactThresholdVolts = (double)_numMinArtifactMv.Value / 1000.0,
            EnableLowPass = _chkLowPass.Checked,
            LowPassCutoff = (double)_numLowPass.Value
        };

    private static int MakeOdd(int value) => value % 2 == 0 ? value + 1 : value;

    private static double StandardDeviation(double[] values, double mean)
    {
        if (values.Length < 2)
            return 0.0;
        double sum = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sum / (values.Length - 1));
    }

    private static double LinearSlopePerSecond(double[] values, double sampleRate)
    {
        if (values.Length < 2 || sampleRate <= 0)
            return 0.0;

        double meanX = (values.Length - 1) / (2.0 * sampleRate);
        double meanY = values.Average();
        double sxx = 0.0;
        double sxy = 0.0;

        for (int i = 0; i < values.Length; i++)
        {
            double x = i / sampleRate;
            sxx += Math.Pow(x - meanX, 2);
            sxy += (x - meanX) * (values[i] - meanY);
        }

        return sxx <= 1e-18 ? 0.0 : sxy / sxx;
    }

    private void UpdateAcquisitionUi()
    {
        if (_btnStart is null)
            return;

        bool measurementReady = _isAcquiring
                             && _minimumMeasurementSamples > 0
                             && _records.Count >= _minimumMeasurementSamples;

        _btnStart.Enabled = !_isAcquiring;
        _btnStop.Enabled = measurementReady;
        _btnStop.Text = !_isAcquiring ? "STOP" : (measurementReady ? "STOP & SAVE POINT" : "COLLECTING...");
        _btnClearSession.Enabled = !_isAcquiring && (_records.Count > 0 || _calibrationPoints.Count > 0);
        _btnSaveRaw.Enabled = measurementReady;
        _cmbChannel.Enabled = !_isAcquiring;
        _grpFilters.Enabled = !_isAcquiring;
        _grpCalibration.Enabled = !_isAcquiring;
        _btnCapturePoint.Enabled = false;
        _btnRemovePoint.Enabled = !_isAcquiring && _calibrationPoints.Count > 0;
        _btnExport.Enabled = !_isAcquiring && _calibrationPoints.Count > 0;

        string bufferText = _totalBufferSize > 0
            ? $" | Buffer: {_totalBufferSize:N0} points"
            : string.Empty;
        SensorDefinition sensor = SelectedSensor;
        _lblSampling.Text = $"{sensor.Name} Ch{sensor.Channel} | Requested: {RequestedSampleRate} Hz | Actual: {_actualSampleRate} Hz\r\n" +
                            $"Display: {DisplayRate} Hz | Range: {InputRange}{bufferText}";
    }

    private SensorDefinition SelectedSensor => _cmbChannel.SelectedIndex == 1 ? AngleSensor : ForceSensor;

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        StopAcquisition();
        FreeBuffer();
    }

    private void FreeBuffer()
    {
        if (_memHandle == IntPtr.Zero)
            return;
        try { MccService.WinBufFreeEx(_memHandle); } catch { }
        _memHandle = IntPtr.Zero;
    }

    private static GroupBox NewGroup(string text, int top, int height)
        => new()
        {
            Text = text,
            Location = new Point(10, top),
            Size = new Size(430, height),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

    private static Label NewLabel(string text, int x, int y, int width)
        => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 23),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Button NewButton(string text, int x, int y, int width, int height)
        => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            UseVisualStyleBackColor = false
        };

    private static TextBox NewReadOnlyText(int x, int y, int width)
        => new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
            ReadOnly = true,
            TextAlign = HorizontalAlignment.Right
        };

    private static CheckBox NewCheckBox(string text, int x, int y, bool isChecked, int width = 350)
        => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 23),
            Checked = isChecked
        };

    private static NumericUpDown NewNumeric(
        int x,
        int y,
        int width,
        decimal minimum,
        decimal maximum,
        decimal value,
        int decimalPlaces,
        decimal increment = 1m)
        => new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            DecimalPlaces = decimalPlaces,
            Increment = increment,
            ThousandsSeparator = false
        };

    private static DataGridViewTextBoxColumn NewTextColumn(
        string header,
        string property,
        int width,
        string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
        if (!string.IsNullOrWhiteSpace(format))
            column.DefaultCellStyle.Format = format;
        return column;
    }

    private readonly record struct SensorDefinition(string Name, int Channel, string Unit);
}
