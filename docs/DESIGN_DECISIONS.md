# MarketDataSystem – Design Decisions (ADR-Style)

This document captures the **key architectural decisions** behind the MarketDataSystem.

Each section follows a simplified ADR format:

- **Context**
- **Decision**
- **Alternatives**
- **Consequences**

---

## 1. Clean Architecture & CQRS

### Context

We need a system that is:

- Easy to reason about,
- Easy to test,
- Flexible enough to swap infrastructure (e.g., persistence, queues),
- Still compact enough for a coding assignment.

### Decision

Use a **Clean Architecture**-like structure with **CQRS**:

- `MarketData.API` – HTTP layer (controllers, DI setup, Swagger).
- `MarketData.Application` – Commands, queries, DTOs, interfaces.
- `MarketData.Domain` – Core entities (`PriceUpdate`, `SymbolStatistics`, `PriceAnomaly`).
- `MarketData.Infrastructure` – Processor, repositories, data structures, simulation.

**Commands** (`ProcessPriceUpdateCommand`) mutate state.  
**Queries** (`GetSymbolStatisticsQuery`, `GetAllStatisticsQuery`, `GetRecentAnomaliesQuery`) read state.

### Alternatives

- Put everything in a single project.
- Mix controller logic and business logic in the same classes.
- Use a heavy framework (e.g., DDD with aggregates + rich domain services).

### Consequences

- **Pros**:
  - Clear separation of concerns.
  - Easy to test Application and Domain layers.
  - Easy to swap implementations (e.g., in-memory repo ↔ database repo).
- **Cons**:
  - More projects and files than a “toy” solution.
  - Slightly more boilerplate (interfaces, DTOs, etc.).

---

## 2. HighPerformanceMarketDataProcessorService & Channel-based Partitioning

### Context

We need to handle **10,000+ ticks per second**, suspicious spikes, and maintain per-symbol state in memory.

### Decision

Implement `HighPerformanceMarketDataProcessorService` with:

- **Partitioned channels**: `Channel<PriceUpdate>[]`.
- **One worker per partition**.
- **Single-writer** per partition:
  - Each symbol is routed to exactly one worker by `hash(symbol) % partitionCount`.

### Alternatives

- Single-threaded processor with a global lock.
- `ConcurrentQueue` with multiple consumer tasks.
- TPL Dataflow blocks or Rx streams.

### Consequences

- **Pros**:
  - Scales with CPU cores via partitions.
  - Single-writer pattern simplifies concurrency.
  - Channels give backpressure and non-blocking behavior.
- **Cons**:
  - Partitioning adds a layer of complexity.
  - Requires careful design of shutdown and error handling.

---

## 3. Bounded Channels with DropOldest

### Context

We must protect the system against unbounded memory growth under extreme load.

### Decision

Use **bounded** channels with `FullMode = DropOldest`:

- Each partition channel has a configurable capacity.
- When full, the oldest item in that channel is dropped to accept new data.

### Alternatives

- `DropNewest`: keep old items, drop new ones.
- `Wait`: block producers until space is available.
- Unbounded channels (risk of memory pressure).

### Consequences

- **Pros**:
  - System remains stable under overload.
  - Real-time stream semantics: newest data is usually more important.
- **Cons**:
  - Under extreme overload, some ticks are dropped.
  - Not suitable if **every** tick must be processed (e.g., strict accounting).

---

## 4. MovingAverageBuffer – Ring Buffer for Moving Average

### Context

Per symbol, we need a moving average over the last N ticks, in **constant time** and space.

### Decision

Implement `MovingAverageBuffer`:

- Fixed-size array of `double`.
- Maintain rolling **sum** and **index**.
- On add:
  - If buffer not full: append.
  - Otherwise: subtract oldest, add new, overwrite slot.

### Alternatives

- Recompute average by scanning a dynamic list each time.
- Keep a full list of all historical values.

### Consequences

- **Pros**:
  - O(1) update, O(1) memory.
  - No per-tick allocations.
- **Cons**:
  - Only keeps last N values; older values are forgotten.
  - N is global (config-based), not symbol-specific (in this sample).

---

## 5. SlidingWindow – Monotonic Deques for 1s Window

### Context

Requirement: detect spikes **greater than 2% within any 1-second window**.

### Decision

Implement `SlidingWindow` using **two monotonic deques**:

- `_minDeque`: non-decreasing prices → head is current min.
- `_maxDeque`: non-increasing prices → head is current max.
- Each entry is `(timestampMs, price)`.

Operations:

- On new tick:
  - Evict entries older than `timestampMs - windowMs`.
  - Maintain monotonic order by popping from tail.
- To query:
  - Evict old entries again using current time.
  - Read head of `_minDeque` and `_maxDeque`.

### Alternatives

- Use a simple list and scan it for min/max each time.
- Use a full-blown time-series database.

### Consequences

- **Pros**:
  - O(1) amortized operations.
  - Strict 1-second window semantics.
  - No heavy external dependency.
- **Cons**:
  - Implementation is more complex than a naive list.
  - Does not directly support advanced statistical detection (e.g., z-scores) – but can be extended.

---

## 6. In-memory Repositories (Statistics & Anomalies)

### Context

We need to support simple queries and keep architecture clean, but the assignment does not require persistence.

### Decision

Define interfaces:

- `IStatisticsRepository`
- `IAnomalyRepository`

Provide in-memory implementations:

- `InMemoryStatisticsRepository` – projects processor state into queryable snapshots.
- `InMemoryAnomalyRepository` – bounded queue storing recent anomalies.

### Alternatives

- Directly query the processor from controllers.
- Persist state/anomalies to a database.

### Consequences

- **Pros**:
  - Clean separation between processor and API.
  - Easy to add a DB-backed repository later.
- **Cons**:
  - Data is ephemeral (lost on restart).
  - In-memory only; no long-term history.

---

## 7. Simulation Hosted Service

### Context

Requirement: “simulate the feed with random price updates for multiple symbols.”

We want an **autonomous demo mode** with no external dependencies.

### Decision

Implement `SimulatedMarketDataFeedHostedService`:

- Reads configuration from `MarketDataProcessingOptions.Simulation`.
- Maintains a `decimal[]` array of prices per symbol.
- Applies a random-walk jitter each interval.
- Pushes `PriceUpdate` to the processor at a configurable ticks-per-second rate.

### Alternatives

- Implement a CLI-only simulator.
- Require a real external data source.

### Consequences

- **Pros**:
  - Easy to demo the system without any extra setup.
  - Handy for local load testing.
- **Cons**:
  - Not realistic market data.
  - Must be disabled in production (or tuned carefully).

---

## 8. Decimal in Domain, Double in Analytics

### Context

Financial values should avoid surprising rounding errors; performance-sensitive analytics need speed.

### Decision

- Domain entities (`PriceUpdate`, `SymbolStatistics`, `PriceAnomaly`) use **decimal**.
- Analytics (`MovingAverageBuffer`, `SlidingWindow`) use **double** internally.

### Alternatives

- Use decimal everywhere.
- Use double everywhere.

### Consequences

- **Pros**:
  - Domain-facing values are precise (`decimal`).
  - Analytics calculations are faster (`double`).
- **Cons**:
  - Requires conversion back and forth.
  - Small approximation error in analytics path (acceptable for spikes detection).

---

These decisions together form the **backbone of the system design** and can be explained confidently in an interview or design review.


---

## 9. Use of MediatR vs Direct Service Calls

### Context

The API controllers could directly depend on application services (e.g., `IPriceUpdateService`),
but we also want:

- A consistent pattern for commands & queries,
- Easy insertion of cross-cutting concerns (logging, metrics, validation) later.

### Decision

Use **MediatR** as a lightweight mediator between controllers and handlers.

### Consequences

- Controllers stay very thin.
- Handlers each represent a **single use case**, making reasoning and testing easier.
- It is straightforward to add pipeline behaviors later (for logging, metrics, etc.).
