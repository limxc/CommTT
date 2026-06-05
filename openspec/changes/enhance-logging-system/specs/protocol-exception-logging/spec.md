## ADDED Requirements

### Requirement: Protocol exception capture at critical boundaries only
The system SHALL record exceptions occurring in `ProtocolDispatcher.OnBufferRead`, `SerialCommProvider.ConnectAsync`, `SerialCommProvider.SendAsync`, and `SerialCommProvider.ReadLoopAsync` into the protocol exception log. Normal frame dispatch SHALL NOT trigger protocol exception logging.

#### Scenario: Protocol parse failure
- **WHEN** `ProtocolDispatcher` encounters a parsing exception
- **THEN** a protocol exception log line is written to `Logs/<YYYYMMDD>/protocol-exception.txt` in plain text format, containing timestamp, protocol type, port name, exception type, message, context, and truncated raw data snapshot

#### Scenario: Serial connect failure
- **WHEN** `SerialCommProvider.ConnectAsync` throws due to port unavailable
- **THEN** a protocol exception log line is created with the exception details and current serial config context

### Requirement: Protocol exception log file format and location
The system SHALL persist protocol exception logs to daily TXT files at `<BaseDirectory>/Logs/<YYYYMMDD>/protocol-exception.txt`, using plain text format with fixed delimiter layout (timestamp | protocol | port | exception type | message | context | raw data), with daily directory creation and no automatic deletion.

#### Scenario: File creation on first exception
- **WHEN** the first exception of a new day is logged
- **THEN** the system creates the `Logs/<YYYYMMDD>/` directory and `protocol-exception.txt`, then appends the plain text line

### Requirement: Context in plain text protocol exception logs
The system SHALL include ambient contextual properties (e.g., current connection config snapshot) in plain text format within the protocol exception log line.

#### Scenario: Serial port config context
- **WHEN** an exception occurs during serial communication
- **THEN** the log line includes context such as `PortName=COM3,BaudRate=9600,Parity=None,DataBits=8`
