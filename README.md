Cervical Manipulation Training & Analysis System
This repository provides a comprehensive solution for cervical manipulation training, consisting of two integrated modules designed under the Cyriax manual therapy paradigm. The system addresses "harm anxiety" by providing objective, real-time feedback and post-session kinematic analysis.

📦 System Modules
1. Cervical Force Simulator (Acquisition)
A real-time GUI for monitoring and recording cervical manipulation parameters.

High-Speed DAQ: Integrated with MCC USB-1608FS (1000 Hz sampling).

Real-time Processing: 2nd order Butterworth filtering for clean signal visualization.

Target Zones: Visual guides for traction force (kgf) and rotation limits.

2. Cervical Analyzer (Post-Processing)
A specialized tool for evaluating recorded manipulation sessions.

Manual Marker Placement: Identify key phases: Traction Start, Rotation Start, Rotation Peak, and Manipulation (Thrust).

Automated Clinical Reporting: Logical validation of the "Traction-first" sequence.

Error Detection: Automatic warnings if the thrust occurs before the rotation peak or if the force falls outside the target zone.

🛠 Technical Specifications
Language: C# (.NET Framework)

Signal Processing: Real-time Butterworth Low-Pass Filter.

Data Format: JSON-based structured data export (Time, Force, Angle).

Paradigm: Optimized for the Cyriax approach (Traction -> Rotation -> Thrust).

🚀 Hardware & Requirements
Hardware: MCC USB-1608FS DAQ, S-type Load Cell, Analog Encoder.

Software: Measurement Computing Universal Library (MccDaq).

Environment: Visual Studio 2022 / .NET Desktop Development.

📂 Project Structure
/CervicalForceSim: Source code for the data acquisition software.

/CervicalAnalyzer: Source code for the kinematic analysis and reporting tool.

Manipulasyon_20260127.json: Sample dataset for testing the analyzer.

## 📜 Citation & Academic Use

If you use this system in your research or clinical training, please cite it as:

> **IRMAK, R.** (2026). *A Simulator and Analysis Tool Design in the Context of Harm Anxiety in Cervical Manipulation Training* (Version 1.2.0). Zenodo. https://doi.org/10.5281/zenodo.18405267

⚖️ License
Distributed under the CC BY 4.0 (Creative Commons Attribution 4.0 International).
