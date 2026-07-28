using System;
using System.Collections.Generic;

namespace CervicalCalibrationTool;

public sealed class SampleRecord
{
    public long SampleIndex { get; init; }
    public double TimeSeconds { get; init; }
    public ushort Adc { get; init; }
    public double RawVoltage { get; init; }
    public double NotchVoltage { get; init; }
    public double FilteredVoltage { get; init; }
    public bool Artifact { get; init; }
}

public sealed class CalibrationPoint
{
    public string Sensor { get; init; } = string.Empty;
    public int Channel { get; init; }
    public string Unit { get; init; } = string.Empty;
    public double KnownValue { get; init; }
    public double MeanRawVoltage { get; init; }
    public double MeanFilteredVoltage { get; init; }
    public double StandardDeviationVolts { get; init; }
    public double PeakToPeakVolts { get; init; }
    public double DriftVoltsPerSecond { get; init; }
    public double ArtifactFraction { get; init; }

    public double StandardDeviationMillivolts => StandardDeviationVolts * 1000.0;
    public double PeakToPeakMillivolts => PeakToPeakVolts * 1000.0;
    public double DriftMillivoltsPerSecond => DriftVoltsPerSecond * 1000.0;
    public double ArtifactPercent => ArtifactFraction * 100.0;
    public int SampleCount { get; init; }
    public DateTime CapturedAt { get; init; } = DateTime.Now;
}
