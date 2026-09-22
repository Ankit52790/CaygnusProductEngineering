# Product Engineering Challenge Submission

## 1. Candidate

- Name: Ankit Kumar
- Email: aankitkumar527909@gmail.com
- GitHub: https://github.com/Ankit52790
- Selected problem: Problem 1 - Resumable Realtime Conversation
- Demo video: Add final demo video link if required

---

## 2. Project

This submission implements **Problem 1: Resumable Realtime Conversation**.

The system consists of:

- Angular frontend
- ASP.NET Core Web API
- Entity Framework Core persistence
- Durable ordered run events
- Server-Sent Events (SSE)
- Deterministic fake response generation
- Cursor-based replay
- Sequence-based deduplication
- Automatic reconnect
- Run recovery after application restart
- Partial-output failure handling

---

## 3. How to Run

### Backend

From the repository root:

```powershell
cd backend/Caygnus.ResumableConversation.Api/Caygnus.ResumableConversation.Api

dotnet run
```

### Frontend

From the repository root:

```powershell
cd client/caygnus-client

npm ci

npm start
```

The Angular client expects the backend at:

```text
https://localhost:7269/api
```

---

## 4. Automated Tests

Run:

```powershell
cd tests/Caygnus.Tests

dotnet test
```

Current verified result:

```text
Test summary: total: 12, failed: 0, succeeded: 12, skipped: 0
Build succeeded
```

The test suite covers:

- ordered broker delivery
- unsubscribe behavior
- ordered durable run events
- resuming from persisted tokens without duplication
- generation failure after partial output
- recovery of persisted running runs
- unknown run handling
- negative cursor validation
- stale cursor handling
- cursor-based replay
- terminal-event replay
- complete deterministic benchmark processing

---

## 5. Build Verification

The backend was independently built using:

```powershell
dotnet build .\backend\Caygnus.ResumableConversation.Api\Caygnus.ResumableConversation.Api\Caygnus.ResumableConversation.Api.csproj
```

Result:

```text
Build succeeded
```

The Angular application was also built using:

```powershell
cd client/caygnus-client
npm run build
```

Result:

```text
Application bundle generation complete.
```

Generated build output is not intended to be committed to the repository.

---

## 6. Verification Benchmark

The deterministic response generator currently produces a 42-event run:

```text
1    run.started
2-41 token events
42   run.completed
```

The automated benchmark observed:

```text
Benchmark observed event count: 42
Benchmark final state: Completed
```

The reconstructed response was also successfully produced.

This verifies the ordered 42-event processing path and exceeds the assignment requirement of at least 30 ordered text events.

### Live interruption/reconnection benchmark

The actual live interruption/reconnection benchmark has not yet been measured.

After performing the live test, record the observed values here:

```text
Expected events: 42
Initial events received: <observed>
Interruption: yes
Last processed sequence before interruption: <observed>
Reconnect cursor: <observed>
Events replayed after reconnect: <observed>
Final unique events received: <observed>
Missing events: <observed>
Duplicate logical events: <observed>
Final run state: <Completed/Failed>
```

No unobserved benchmark values are claimed.

---

## 7. Acceptance Scenarios

### AC1 - Ordered live stream

The backend assigns sequence numbers to durable run events.

Each event is persisted before being published to the in-memory live broker.

The SSE stream emits events using the persisted sequence.

The Angular client processes events using increasing sequence numbers.

The automated benchmark verifies a complete sequence from:

```text
1 through 42
```

---

### AC2 - Missed-event recovery

The client stores its latest successfully processed sequence.

When reconnecting, it requests:

```text
/api/runs/{runId}/stream?after={sequence}
```

The backend queries durable events whose sequence is greater than the supplied cursor and replays them in order.

The client can therefore recover events that were persisted while it was disconnected.

---

### AC3 - Replay/live overlap

The server subscribes to the live broker before reading the persisted event history.

This reduces the race window between replay and live subscription.

The server ignores events whose sequence is already covered by the replay cursor.

The Angular client also performs sequence-based deduplication.

This provides a second protection layer against replay/live overlap.

---

### AC4 - Service restart

`RunRecoveryService` runs when the application starts.

It searches for persisted runs whose state is:

```text
RunStatus.Running
```

and starts `RunProcessor` for each one.

`RunProcessor` reads the already-persisted event history and continues generation from the persisted token count.

Previously persisted output is therefore not regenerated as new token events.

The automated recovery test verifies successful recovery and completion.

---

### AC5 - Generation failure

If generation fails after partial output:

1. previously persisted token events remain in the database
2. the run is changed to `RunStatus.Failed`
3. a `run.failed` event is persisted
4. the failure reason is stored on the run

The automated failure test verifies:

```text
1  run.started
2  token: partial
3  run.failed
```

and verifies that the final run state is:

```text
RunStatus.Failed
```

---

### AC6 - Unknown or stale cursor

The stream endpoint validates the run ID and cursor.

The implementation handles:

- unknown run ID with HTTP 404
- invalid negative cursor with HTTP 400
- stale cursor with an explicit HTTP 409 response
- valid cursors through normal event replay

The automated stream controller tests verify these cases.

---

## 8. Architecture and Data Flow

```text
Angular Client
      |
      | HTTP
      | SSE
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

The database is the durable source of truth.

The event broker is responsible only for transient delivery to currently connected clients.

---

## 9. Event and Cursor Model

Each `RunEvent` contains:

- event ID
- run ID
- sequence
- event type
- optional text
- creation timestamp

The sequence is monotonically increasing within a run.

The sequence also acts as the client checkpoint.

Example:

```text
Client has processed sequence 17.
```

The next connection uses:

```text
after=17
```

The server replays:

```text
18, 19, 20, ...
```

from durable storage.

The SSE `id` field carries the event sequence.

---

## 10. Ordering

Ordering is owned by the persisted run-event sequence.

Before an event is published:

```text
create event
    ↓
persist event
    ↓
publish event
```

The database therefore provides the durable ordering source.

---

## 11. Replay and Live Delivery

The stream controller intentionally subscribes before replaying:

```text
1. Subscribe to live broker
2. Query durable events after cursor
3. Replay persisted events
4. Continue receiving live events
```

This reduces the possibility of missing an event generated between replay and subscription.

The server ignores replay/live duplicates using sequence comparison.

The client performs an additional sequence comparison before updating the UI.

---

## 12. Deduplication

Deduplication is sequence-based.

The backend ignores events that are already covered by the replay position.

The Angular service maintains:

```typescript
currentSequence
```

and ignores events where:

```typescript
event.sequence <= currentSequence
```

The UI therefore only processes events that advance the checkpoint.

---

## 13. Service Restart

The application registers `RunRecoveryService` as a hosted service.

On startup it searches for:

```text
RunStatus.Running
```

runs.

Each discovered run is passed to `RunProcessor`.

Because `RunProcessor` counts persisted token events before continuing generation, previously persisted output is not regenerated as new token events.

The recovery test verifies successful completion after recovery.

---

## 14. Reconnect Behaviour

The Angular client uses browser `EventSource`.

When the SSE connection fails:

```text
SSE error
   ↓
close EventSource
   ↓
status = Reconnecting
   ↓
wait 1 second
   ↓
connect using currentSequence
```

The reconnect delay is currently a fixed one second.

The client reconnects using the latest successfully processed event sequence rather than resetting to sequence zero.

---

## 15. Failure Handling

The deterministic generator makes failure behavior reproducible.

When a generator exception occurs:

```text
RunStatus.Failed
```

is persisted.

A `run.failed` event is also appended to durable history.

Previously persisted token events remain available.

The automated failure test verifies this behavior.

---

## 16. Technology Choices

### ASP.NET Core

Used for the backend HTTP API, run processing, and SSE endpoint.

### Entity Framework Core

Used for durable storage of conversations, messages, runs, and run events.

### SQL Server

Used by the application runtime as the persistent database provider.

### Server-Sent Events

SSE was selected because this problem primarily requires server-to-client streaming.

The browser `EventSource` API also provides a simple client implementation.

### Angular

Used for the browser client and connection-state UI.

### Deterministic Fake Generator

Used to make automated testing and demonstration repeatable without requiring an external paid model API.

---

## 17. Production Considerations

The implementation is intentionally scoped to the assignment.

A production deployment would additionally require consideration of:

- authentication and authorization
- event retention and expiry
- database indexing
- distributed background processing
- multiple API instances
- distributed event delivery
- connection limits
- observability
- retry policies
- backpressure
- operational monitoring

---

## 18. Event Retention Discussion

If old run events are removed through retention policies, a client whose cursor points to an event older than the retained history may no longer have enough information to reconstruct the missing events.

The service should explicitly identify that cursor as stale instead of silently returning an incomplete response.

Possible production approaches include:

- longer retention
- snapshots/checkpoints
- archival event storage
- explicit client resynchronization

---

## 19. Important Trade-off

The implementation uses a bounded in-memory channel for each live subscriber.

This prevents unlimited memory growth caused by slow consumers.

The trade-off is that a very slow consumer may lose buffered live notifications if the channel reaches capacity.

This does not make the durable history unavailable because the database remains the source of truth.

After reconnecting, the client can request events after its last processed sequence and recover persisted events.

---

## 20. Limitations

The current prototype does not implement:

- authentication/authorization
- multiple simultaneous assistant runs
- user cancellation
- multiple production servers
- multi-region ordering
- internet-scale load testing

The live interruption/reconnection benchmark still needs to be executed and recorded.

---

## 21. AI Usage

AI assistance was used during development for:

- discussing architecture and implementation approaches
- debugging and resolving implementation issues
- reviewing test coverage
- refining documentation and submission materials

The application code, local builds, automated tests, and verification results were executed and checked in the development environment.

No claim is made that AI independently developed or verified the entire project.

---

## 22. Demo Video

Add the final demo video link if the assignment requires one.

The demo should show:

1. conversation creation
2. streamed response
3. connection-state changes
4. interruption during generation
5. reconnection
6. cursor-based recovery
7. failure or restart behavior
8. benchmark result
9. architecture explanation
10. one implementation trade-off

---

## 23. Credibility Note

The implementation and automated test results reported in this document are based on the submitted source code and observed local execution results.

The automated verification currently reports:

```text
12 tests passed
42 benchmark events observed
Final benchmark state: Completed
Backend build succeeded
Angular build succeeded
```

Live interruption/reconnection figures should only be added after they have been directly observed during the live benchmark.