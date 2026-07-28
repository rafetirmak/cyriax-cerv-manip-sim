# Single-Channel Calibration Data Collector v7.3

A Windows Forms data-collection utility for one USB-1608FS analog input channel at a time.

## Measurement workflow

1. Select Force Channel 0 or Angle Channel 1.
2. Enter the known reference value.
3. Apply the load before pressing START.
4. The program calculates the minimum acquisition duration automatically.
5. STOP remains disabled until the settling interval and stable measurement window are complete.
6. STOP saves one accepted measurement point and clears the temporary DAQ/raw/filter buffers.
7. Repeat for each load.
8. Use **EXPORT SAVED POINTS (.CSV)** to save every accepted row in the table.

## Output behavior

- No regression equation is calculated.
- No calibration JSON is created.
- **EXPORT SAVED POINTS (.CSV)** exports the accepted table rows.
- **SAVE CURRENT RAW (.CSV)** optionally exports the complete current START/STOP raw trace before STOP is pressed.
- Raw CSV export creates only a CSV file; it does not create a companion metadata JSON.

## Signal processing

Raw voltage -> 50 Hz notch -> Hampel spike suppression -> fourth-order low-pass filter.

Measurement-point acceptance uses the final filtered signal for SD, peak-to-peak value, and drift. Raw and intermediate signals remain available only in the optional raw-trace CSV.

## Default acquisition settings

- Requested sampling rate: 1000 Hz
- Stable window: 5 seconds
- Automatic settling interval: normally 1 second with the default filters
- Default minimum acquisition duration: normally 6 seconds
- STOP is disabled until enough samples have been collected

## Requirements

- Windows 10/11 x64
- Visual Studio 2022
- .NET 8 desktop development workload
- Measurement Computing Universal Library and USB-1608FS driver

Open `CervicalCalibrationTool.sln`, select `Debug | x64`, and run the project.
