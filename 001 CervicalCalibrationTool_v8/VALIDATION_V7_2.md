# V7.2 validation notes

- Stability source: final filtered voltage.
- Stop gate: enabled only after automatic minimum sample count.
- Analysis window: final stable window only; warm-up samples excluded.
- Per START: temporary records cleared, new DAQ buffer allocated, new filter pipeline created.
- Per STOP: accepted point retained; temporary records, graph, filter pipeline and DAQ buffer cleared.
- Static C# delimiter/string/comment check: passed.
- Synthetic test (1000 Hz, 1 s transient + 5 s stable):
  - final-filtered SD = 0.141 mV, accepted under 2.000 mV limit;
  - raw SD = 70.711 mV, demonstrating why raw/notch-only stability rejection was inappropriate for the displayed stable output.

A physical USB-1608FS and .NET SDK were not available in the build environment, so final hardware execution must be verified in Visual Studio.
