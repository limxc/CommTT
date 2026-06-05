## ADDED Requirements

### Requirement: Protocol state transition recording
The system SHALL record every `ConnectionState` transition event for each registered `ICommProvider` into the protocol state log, capturing old state, new state, trigger reason, and timestamp.

#### Scenario: Serial connect transition
- **WHEN** `SerialCommProvider.ConnectAsync` successfully opens the port
- **THEN** a state log line is appended to `Logs/<YYYYMMDD>/protocol-state.txt` in plain text format with timestamp, protocol type, connection ID, old state, new state, trigger reason, and details

#### Scenario: Disconnect transition
- **WHEN** `SerialCommProvider.DisconnectAsync` is invoked or the read loop terminates
- **THEN** a state log line is appended with `NewState=Disconnected` and `TriggerReason` equal to `UserAction` or `IoException` accordingly

### Requirement: Protocol state log file format and location
The system SHALL persist protocol state logs to daily TXT files at `<BaseDirectory>/Logs/<YYYYMMDD>/protocol-state.txt`, using plain text format with fixed delimiter layout (timestamp | protocol | connection ID | old state → new state | trigger reason | details), with daily directory creation and no automatic deletion.

#### Scenario: File creation on first state change
- **WHEN** the first state transition of a new day occurs
- **THEN** the system creates the `Logs/<YYYYMMDD>/` directory and `protocol-state.txt`, then appends the plain text line

### Requirement: Trigger reason classification
The system SHALL classify every state transition with a `TriggerReason` enum value: `UserAction`, `IoException`, `ProtocolError`, `Timeout`, or `GracefulClose`.

#### Scenario: Read loop failure
- **WHEN** the serial read loop terminates due to an `IOException`
- **THEN** the resulting state log line uses `TriggerReason=IoException`
