# Caygnus Product Engineering Assignment

## Overview

This project implements **Problem 1: Resumable Realtime Conversation**.

The system provides a resumable conversational response flow using:

- ASP.NET Core Web API
- Entity Framework Core
- SQL Server persistence
- Server-Sent Events (SSE)
- Durable ordered run events
- Sequence-based replay and deduplication
- Automatic client reconnection
- Run recovery after application restart
- Angular frontend
- Deterministic fake response generation
- Automated .NET tests

The database is the durable source of truth for run history. SSE is used for realtime delivery to connected clients, while persisted run events allow disconnected clients to replay missed events.

---

## Project Structure

```text
CaygnusProductEngineering/
├── backend/
│   └── Caygnus.ResumableConversation.Api/
│       └── Caygnus.ResumableConversation.Api/
├── client/
│   └── caygnus-client/
├── tests/
│   └── Caygnus.Tests/
├── README.md
├── SUBMISSION.md
└── .gitignore
```

---

## Architecture

The system uses durable run events as the source of truth.

```text
Angular Client
      |
      | HTTP + SSE
      v
ASP.NET Core API
      |
      +----------------------+
      |                      |
      v                      v
 RunProcessor          EventStreamBroker
      |
      v
 AppDbContext
      |
      +-----------------------------+
      |              |              |
      v              v              v
Conversations     Messages        Runs
                                   |
                                   v
                                RunEvents
```

A generated response is represented as a sequence of durable events.

Example:

```text
1  run.started
2  token
3  token
4  token
...
41 token
42 run.completed
```

Each event is persisted before it is published to connected SSE clients.

---

## Backend

The backend is an ASP.NET Core Web API.

Main responsibilities include:

- conversation creation
- user message creation
- run creation
- deterministic response generation
- durable event persistence
- ordered event sequencing
- SSE streaming
- cursor-based event replay
- run recovery after restart
- explicit completed and failed run states

### Run Processing

`RunProcessor`:

1. Loads the persisted run.
2. Creates `run.started` when necessary.
3. Reads already-persisted events.
4. Continues generation after already-persisted tokens.
5. Assigns the next sequence number.
6. Persists each event before publishing it.
7. Persists `run.completed` when generation succeeds.
8. Persists `run.failed` when generation fails.

This allows a partially generated run to retain its existing event history.

---

## Restart Recovery

`RunRecoveryService` runs during application startup.

It finds runs persisted with:

```text
RunStatus.Running
```

and invokes `RunProcessor` for those runs.

Because the processor resumes from persisted token events, an interrupted run can continue from its existing durable history instead of depending on in-memory state.

---

## Frontend

The frontend is an Angular application located at:

```text
client/caygnus-client/
```

Install dependencies:

```powershell
cd client/caygnus-client
npm ci
```

Run the frontend:

```powershell
npm start
```

Build the frontend:

```powershell
npm run build
```

The Angular client expects the backend at:

```text
https://localhost:7269/api
```

The UI displays:

- conversation ID
- run ID
- latest event sequence
- connection state
- streamed assistant response
- reconnect state
- completed state
- failure state
- manual reconnect control

---

## Realtime Transport

The implementation uses **Server-Sent Events (SSE)**.

The stream endpoint is:

```text
GET /api/runs/{runId}/stream?after={sequence}
```

The client uses the browser `EventSource` API.

Named SSE events include:

```text
run.started
token
run.completed
run.failed
```

SSE was selected because the communication required by this assignment is primarily server-to-client streaming, while normal HTTP requests are sufficient for creating conversations and submitting messages.

---

## Event and Cursor Model

Each `RunEvent` has a monotonically increasing sequence number within its run.

The sequence number is used as the client replay cursor.

For example:

```text
last processed event = 17
```

The client reconnects with:

```text
?after=17
```

The server then replays persisted events whose sequence is greater than `17`.

The SSE `id` field carries the event sequence.

---

## Replay and Live Delivery

The server subscribes the SSE client to the live event broker before querying persisted events.

The flow is:

```text
Subscribe to live broker
        |
        v
Replay durable events after cursor
        |
        v
Continue receiving live events
```

This ordering reduces the race window where an event could otherwise be generated between the replay query and live subscription.

The server also ignores events whose sequence is already at or below the replay cursor.

The Angular client performs an additional sequence check and ignores events that have already been processed.

---

## Client Reconnection

When the SSE connection fails, the Angular client:

1. closes the failed `EventSource`
2. changes the UI state to `Reconnecting`
3. waits one second
4. reconnects using the latest successfully processed sequence

The reconnect request therefore resumes from the client's current checkpoint rather than starting from the beginning.

The reconnect delay is currently a fixed one second.

---

## Deduplication

Deduplication is sequence-based.

The backend ignores a live event when:

```text
event.Sequence <= current replay position
```

The Angular client independently ignores an event when:

```text
event.sequence <= currentSequence
```

This protects the UI from replay/live overlap and repeated delivery.

---

## Persistence

The backend persists:

- conversations
- messages
- runs
- run events

The database is the durable source of truth for event history.

The in-memory `EventStreamBroker` is only used for transient live notification of connected clients.

---

## Cursor Validation

The stream endpoint validates the requested cursor.

The implementation handles:

- unknown run IDs
- invalid negative cursors
- stale cursors outside the available event history
- normal replay from a valid cursor

A stale cursor is represented as an explicit recoverable error rather than silently returning an incomplete event history.

The test suite includes coverage for these stream validation scenarios.

---

## Deterministic Response Generator

The project uses a deterministic fake response generator.

No paid external model API is required.

This makes response generation repeatable for automated tests and local demonstration.

The current automated processor benchmark produces a 42-event run:

```text
1  run.started
2-41 token events
42 run.completed
```

---

## Tests

The automated tests are located at:

```text
tests/Caygnus.Tests/
```

Run:

```powershell
cd tests/Caygnus.Tests
dotnet test
```

The current verified test run reports:

```text
Test summary: total: 12, failed: 0, succeeded: 12, skipped: 0
Build succeeded
```

The tests cover:

- ordered event delivery through the broker
- unsubscribe behavior
- ordered durable run events
- continuation from persisted token events
- generation failure after partial output
- recovery of persisted running runs
- unknown run handling
- invalid cursor handling
- stale cursor handling
- cursor-based replay
- terminal-event replay
- complete 42-event benchmark processing

---

## Verification Benchmark

The deterministic processor benchmark currently produces:

```text
Expected events: 42
Observed events: 42
Final state: Completed
```

The reconstructed response was also successfully produced from the resulting event sequence.

The 42-event run exceeds the assignment requirement of at least 30 ordered text events.

A separate live benchmark is intended to verify actual connection interruption and reconnection behavior.

That benchmark should record:

```text
Expected events
Initial events received
Last processed sequence before interruption
Reconnect cursor
Events replayed after reconnect
Final unique events received
Missing events
Duplicate logical events
Final run state
```

Only observed live benchmark values should be reported as final results.

---

## Production Considerations

The prototype intentionally stays within the scope of the assignment.

A production implementation would additionally consider:

- event retention and expiry
- authentication and authorization
- database indexing
- durable background processing
- distributed event delivery
- multiple server instances
- connection limits
- observability
- retries and backpressure
- horizontal scaling
- operational monitoring

---

## Event Retention

If old run events are removed through retention policies, a client whose cursor points to an event older than the retained history may no longer be able to reconstruct the complete response from the event store.

A production implementation should detect this condition and return an explicit stale-cursor response.

Possible approaches include:

- longer event retention
- snapshots/checkpoints
- archival event storage
- explicit client resynchronization

---

## Important Trade-off

The live broker uses bounded in-memory channels.

This keeps live connection memory bounded, but a very slow consumer can cause buffered live notifications to be dropped.

Durable events remain in the database, so a reconnecting client can recover missed events by replaying from its last processed sequence.

This separates durable correctness from transient live delivery.

---

## Technology

### Backend

- ASP.NET Core
- Entity Framework Core
- SQL Server
- Server-Sent Events

### Frontend

- Angular
- TypeScript
- Browser `EventSource`

### Testing

- xUnit
- Entity Framework Core InMemory provider
- Microsoft.NET.Test.Sdk

---

## License

This repository was created as a coding assignment submission.