using System;
using System.Collections.Generic;
using System.Linq;

namespace CervicalCalibrationTool;

/// <summary>
/// A streaming filter chain that always preserves the original raw voltage.
/// Pipeline: raw -> 50 Hz notch -> Hampel transient suppressor -> 4th order low-pass.
/// No high-pass is used because calibration requires preservation of DC/offset values.
/// </summary>
public sealed class SensorFilterPipeline
{
    private Biquad _notch;
    private Biquad _lowPass1;
    private Biquad _lowPass2;
    private HampelFilter _hampel;

    public SensorFilterPipeline(FilterSettings settings)
    {
        Settings = settings.Clone();
        (_notch, _lowPass1, _lowPass2, _hampel) = CreateFilters(Settings);
    }

    public FilterSettings Settings { get; private set; }

    public void Reconfigure(FilterSettings settings)
    {
        Settings = settings.Clone();
        (_notch, _lowPass1, _lowPass2, _hampel) = CreateFilters(Settings);
    }

    public FilterOutput Process(double rawVoltage)
    {
        double notchVoltage = Settings.EnableNotch
            ? _notch.Process(rawVoltage)
            : rawVoltage;

        bool artifactDetected = false;
        double despikedVoltage = Settings.EnableArtifactFilter
            ? _hampel.Process(notchVoltage, out artifactDetected)
            : notchVoltage;

        double filteredVoltage = despikedVoltage;
        if (Settings.EnableLowPass)
        {
            filteredVoltage = _lowPass1.Process(filteredVoltage);
            filteredVoltage = _lowPass2.Process(filteredVoltage);
        }

        return new FilterOutput(
            RawVoltage: rawVoltage,
            NotchVoltage: notchVoltage,
            DespikedVoltage: despikedVoltage,
            FilteredVoltage: filteredVoltage,
            ArtifactDetected: artifactDetected);
    }

    private static (Biquad Notch, Biquad LowPass1, Biquad LowPass2, HampelFilter Hampel)
        CreateFilters(FilterSettings settings)
    {
        if (settings.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings.SampleRate));

        double nyquist = settings.SampleRate / 2.0;
        if (settings.NotchFrequency <= 0 || settings.NotchFrequency >= nyquist)
            throw new ArgumentOutOfRangeException(nameof(settings.NotchFrequency));
        if (settings.LowPassCutoff <= 0 || settings.LowPassCutoff >= nyquist)
            throw new ArgumentOutOfRangeException(nameof(settings.LowPassCutoff));
        if (settings.NotchQ <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings.NotchQ));

        // RBJ biquad notch.
        Biquad notch = Biquad.CreateNotch(
            settings.SampleRate,
            settings.NotchFrequency,
            settings.NotchQ);

        // 4th-order Butterworth low-pass as two cascaded 2nd-order sections.
        Biquad lowPass1 = Biquad.CreateLowPass(
            settings.SampleRate,
            settings.LowPassCutoff,
            q: 0.541196100146197);
        Biquad lowPass2 = Biquad.CreateLowPass(
            settings.SampleRate,
            settings.LowPassCutoff,
            q: 1.306562964876377);

        HampelFilter hampel = new(
            settings.HampelWindow,
            settings.HampelSigma,
            settings.MinimumArtifactThresholdVolts);

        return (notch, lowPass1, lowPass2, hampel);
    }
}

public readonly record struct FilterOutput(
    double RawVoltage,
    double NotchVoltage,
    double DespikedVoltage,
    double FilteredVoltage,
    bool ArtifactDetected);

public sealed class FilterSettings
{
    public double SampleRate { get; set; } = 1000.0;
    public bool EnableNotch { get; set; } = true;
    public double NotchFrequency { get; set; } = 50.0;
    public double NotchQ { get; set; } = 30.0;
    public bool EnableArtifactFilter { get; set; } = true;
    public int HampelWindow { get; set; } = 101;
    public double HampelSigma { get; set; } = 3.5;
    public double MinimumArtifactThresholdVolts { get; set; } = 0.002;
    public bool EnableLowPass { get; set; } = true;
    public double LowPassCutoff { get; set; } = 10.0;

    public FilterSettings Clone() => (FilterSettings)MemberwiseClone();
}

/// <summary>
/// RBJ cookbook biquad. The first sample primes the state so DC signals do not
/// start from zero and create a false calibration transient.
/// </summary>
public sealed class Biquad
{
    private readonly double _b0;
    private readonly double _b1;
    private readonly double _b2;
    private readonly double _a1;
    private readonly double _a2;

    private double _x1;
    private double _x2;
    private double _y1;
    private double _y2;
    private bool _initialized;

    private Biquad(double b0, double b1, double b2, double a1, double a2)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _a1 = a1;
        _a2 = a2;
    }

    public static Biquad CreateNotch(double sampleRate, double frequency, double q)
    {
        double w0 = 2.0 * Math.PI * frequency / sampleRate;
        double alpha = Math.Sin(w0) / (2.0 * q);
        double cos = Math.Cos(w0);

        double b0 = 1.0;
        double b1 = -2.0 * cos;
        double b2 = 1.0;
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cos;
        double a2 = 1.0 - alpha;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    public static Biquad CreateLowPass(double sampleRate, double cutoff, double q)
    {
        double w0 = 2.0 * Math.PI * cutoff / sampleRate;
        double alpha = Math.Sin(w0) / (2.0 * q);
        double cos = Math.Cos(w0);

        double b0 = (1.0 - cos) / 2.0;
        double b1 = 1.0 - cos;
        double b2 = (1.0 - cos) / 2.0;
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cos;
        double a2 = 1.0 - alpha;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    private static Biquad Normalize(
        double b0,
        double b1,
        double b2,
        double a0,
        double a1,
        double a2)
        => new(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);

    public double Process(double input)
    {
        if (!_initialized)
        {
            _x1 = input;
            _x2 = input;
            _y1 = input;
            _y2 = input;
            _initialized = true;
            return input;
        }

        double output = (_b0 * input)
                      + (_b1 * _x1)
                      + (_b2 * _x2)
                      - (_a1 * _y1)
                      - (_a2 * _y2);

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return output;
    }
}

/// <summary>
/// Online trailing-window Hampel filter. It rejects impulsive cable/sensor motion
/// spikes while retaining static force and angle levels. Slow movement is not
/// blindly removed; it is excluded later by the stable-window calibration gate.
/// </summary>
public sealed class HampelFilter
{
    private readonly int _windowSize;
    private readonly double _sigmaMultiplier;
    private readonly double _minimumThreshold;
    private readonly Queue<double> _window;

    public HampelFilter(int windowSize, double sigmaMultiplier, double minimumThreshold)
    {
        if (windowSize < 5)
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        if (windowSize % 2 == 0)
            windowSize += 1;
        if (sigmaMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(sigmaMultiplier));
        if (minimumThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumThreshold));

        _windowSize = windowSize;
        _sigmaMultiplier = sigmaMultiplier;
        _minimumThreshold = minimumThreshold;
        _window = new Queue<double>(windowSize);
    }

    public double Process(double input, out bool artifactDetected)
    {
        _window.Enqueue(input);
        while (_window.Count > _windowSize)
            _window.Dequeue();

        artifactDetected = false;
        if (_window.Count < 7)
            return input;

        double[] values = _window.ToArray();
        double median = Median(values);
        double[] absoluteDeviations = values
            .Select(value => Math.Abs(value - median))
            .ToArray();
        double mad = Median(absoluteDeviations);

        // 1.4826 scales MAD to the standard deviation for Gaussian noise.
        double robustSigma = 1.4826 * mad;
        double threshold = Math.Max(
            _minimumThreshold,
            _sigmaMultiplier * robustSigma);

        if (Math.Abs(input - median) <= threshold)
            return input;

        artifactDetected = true;
        return median;
    }

    private static double Median(double[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }
}
