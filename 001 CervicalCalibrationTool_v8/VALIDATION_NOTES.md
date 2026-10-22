# Validation notes — v7.3

## Preserved acquisition behavior

- Hardware acquisition remains at the requested 1000 Hz rate, subject to the rate returned by Universal Library.
- The automatic minimum duration combines filter settling time and the selected stable-window duration.
- STOP remains disabled until the minimum sample count is reached.
- Each START creates a fresh DAQ buffer and resets temporary samples and filter state.
- Each STOP clears the temporary raw trace, graph data, filter state, and DAQ buffer after accepting or rejecting the point.
- The accepted point remains in the table.

## Point acceptance

- Mean raw voltage is calculated from the final stable window.
- Mean filtered voltage is calculated from the final stable window.
- SD, peak-to-peak value, and drift are calculated from the final filtered signal.
- The artifact percentage is retained as a diagnostic field.

## Export behavior

- Regression and equation calculations are absent.
- Calibration JSON export is absent.
- EXPORT SAVED POINTS (.CSV) writes all accepted points.
- SAVE CURRENT RAW (.CSV) writes only the current temporary raw trace and does not create metadata JSON.
