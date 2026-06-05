## ADDED Requirements

### Requirement: Runtime error logging
The system SHALL record software runtime errors, unhandled exceptions, startup/shutdown events, and configuration errors to the runtime log file.

#### Scenario: Unhandled exception in Shell
- **WHEN** an unhandled exception occurs in the WPF Shell main thread
- **THEN** the exception details are written to `Logs/<YYYYMMDD>/runtime.txt` via Serilog

### Requirement: Runtime log human-readable format
The system SHALL write runtime logs in plain text format, each line containing timestamp, level, category, and message, suitable for direct human reading without parsing tools.

#### Scenario: Reading runtime log
- **WHEN** a developer opens `Logs/20260504/runtime.txt` in a text editor
- **THEN** each line is human-readable, e.g. `2026-06-05 20:45:00 [ERR] Bootstrapper: Configuration load failed — System.IO.FileNotFoundException: ...`
