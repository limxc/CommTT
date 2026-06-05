## ADDED Requirements

### Requirement: Logging abstraction based on M.E.L
The system SHALL use `Microsoft.Extensions.Logging.ILogger<T>` as the primary logging abstraction for all application and infrastructure components.

#### Scenario: Constructor injection
- **WHEN** a service is instantiated via DI container
- **THEN** the constructor receives a non-null `ILogger<T>` instance specific to its type

### Requirement: Serilog backend for runtime logs only
The system SHALL configure Serilog as the concrete logging backend during application bootstrap, outputting software runtime observation logs to `Logs/<YYYYMMDD>/runtime.txt` under the application base directory. Serilog SHALL NOT be used for protocol audit logs.

#### Scenario: Bootstrap registration
- **WHEN** the Bootstrapper initializes the Shell module
- **THEN** Serilog is registered as the logging provider and outputs runtime logs to `<BaseDirectory>/Logs/<YYYYMMDD>/runtime.txt`

### Requirement: Runtime log file path and rotation
The system SHALL write runtime logs to a daily TXT file under `Logs/<YYYYMMDD>/runtime.txt`, where `<YYYYMMDD>` is the current local date in `yyyyMMdd` format. The system SHALL NOT automatically delete historical runtime log files.

#### Scenario: Daily directory creation
- **WHEN** the first log event of a new day is emitted
- **THEN** a new directory `Logs/<YYYYMMDD>/` is created and `runtime.txt` is placed inside it
