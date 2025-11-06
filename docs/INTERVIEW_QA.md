# MarketDataSystem – Architecture & Technology Q&A (Very Deep and Extensive)

This document is written in a **Q&A (interview)** style.  
The idea: imagine an experienced interviewer asking you detailed questions about this project, and you are answering.

Use this both as:

- A **self-study guide** to understand all aspects of the system.
- A **cheat sheet** for explaining the project during a real interview.
- A **design review document** that makes your decisions explicit and defendable.

---

## 1. Big Picture

### Q: In one sentence, what does this system do?

A: It is a **real-time market data processing system** that ingests price updates at high throughput,
maintains a **moving average** per symbol, detects **±2% price spikes inside a 1-second window**, and
exposes this state through a **clean, CQRS-based HTTP API**.

### Q: How would you explain this system to a non-technical person?

A: Imagine a service that constantly receives stock or FX prices for many instruments. It keeps track of
recent prices, calculates an average, and raises a flag when a price suddenly jumps or drops in a short
time. Then it provides a simple interface so other systems or dashboards can ask: “What’s the latest state
for symbol X?”

### Q: What is the primary goal of this project from an engineering perspective?

A: The primary goal is to demonstrate a **production-style backend design** that combines:

- Clean layering and separation of concerns.
- Modern .NET 8 patterns (IHostedService, DI, configuration).
- High-throughput, allocation-conscious data processing.
- Clear documentation and justifications for design choices.

It is intentionally closer to a real system than a “toy” coding exercise.

### Q: What are the main non-functional requirements you considered?

A: The main non-functional requirements are:

- **Throughput**: handle at least 10,000 price updates per second.
- **Latency**: keep per-tick processing latency low and predictable.
- **Scalability**: utilize multiple CPU cores via partitioned workers.
- **Correctness**: apply moving average and anomaly detection rules accurately.
- **Testability**: separate business logic from infrastructure, enable unit/integration tests.
- **Observability**: offer metrics that can be used to understand behavior under load.

---

## 2. Architecture & Clean Separation

### Q: Why did you use Clean Architecture and separate into API, Application, Domain, and Infrastructure projects?

A: Clean Architecture helps to keep **business rules independent** from frameworks and infrastructure:

- **Domain** contains core concepts (`PriceUpdate`, `SymbolStatistics`, `PriceAnomaly`) and is free of infrastructure details.
- **Application** defines the use cases (commands and queries) and the interfaces for interacting with the domain.
- **Infrastructure** contains the concrete implementation of `IMarketDataProcessor`, the in-memory repository, and the analytics data structures.
- **API** is an adapter that exposes the use cases via HTTP (ASP.NET Core).

This separation makes the code easier to reason about, test, and evolve. For example, we can replace
the `InMemoryStatisticsRepository` with a database-backed repository without touching the Application or Domain layers.

### Q: How does this structure help when the requirements change?

A: Changes usually fall into categories:

- **New endpoints / different DTOs** → mostly localized to the API and Application layers.
- **New processing logic** (e.g., new anomaly rules) → mostly in the Infrastructure and Domain layers.
- **Persistence requirements** → add or change implementations in Infrastructure and partially Application.

Clean Architecture limits the ripple effect of changes, so most modifications stay inside one layer or a small set of classes.

### Q: Why did you introduce `IMarketDataProcessor` and `IStatisticsRepository` instead of using the concrete processor directly in the API?

A: This aligns with the **dependency inversion principle (DIP)**:

- Application knows about abstractions (`IMarketDataProcessor`, `IStatisticsRepository`) but not concrete types.
- Infrastructure implements these abstractions.

Benefits:

1. **Testability** – in unit tests, we can mock or stub these interfaces.
2. **Replaceability** – we can swap the implementation (e.g., from in-memory processor to one backed by Kafka) with minimal changes.
3. **Decoupling** – prevents Application and Domain logic from depending directly on ASP.NET Core or low-level threading details.

### Q: How does the dependency flow look across projects?

A: It is strictly **inward**:

- `MarketData.API` depends on `MarketData.Application` and `MarketData.Infrastructure`.
- `MarketData.Application` depends on `MarketData.Domain`.
- `MarketData.Infrastructure` depends on `MarketData.Application` (for interfaces) and `MarketData.Domain`.
- `MarketData.Domain` depends on nothing else in the solution.

This matches the classic Clean Architecture rule: outer layers depend on inner layers, but not vice versa.

---

## 3. API and CQRS

### Q: What endpoints does the API expose, and how do they map to commands and queries?

A:

- `POST /api/prices` → `ProcessPriceUpdateCommand`
  - Accepts a JSON payload with `symbol`, `price`, and optional `timestamp`.
  - Handler creates a `PriceUpdate` and calls `IMarketDataProcessor.EnqueueUpdateAsync`.

- `GET /api/prices/{symbol}` → `GetSymbolStatisticsQuery`
  - Returns a `SymbolStatisticsDto` with the latest aggregated view for that symbol.

Internally, the API uses **MediatR** to implement the CQRS pattern:

- Commands modify state (`ProcessPriceUpdateCommand`).
- Queries read state (`GetSymbolStatisticsQuery`).

### Q: Why did you choose MediatR for CQRS instead of directly calling services from controllers?

A: MediatR provides a lightweight **request/response mediator** that:

- Decouples controllers from the specific handler implementations.
- Makes each use case (command/query) explicit and easy to find.
- Fits well with pipeline behaviors (e.g., for logging, validation), even though this demo only uses validation directly.

It also mirrors patterns commonly used in larger microservices where commands and queries are central to behavior.

### Q: How did you handle validation?

A:

- I used **FluentValidation** to define validation rules for `ProcessPriceUpdateCommand`.
- For example:
  - `Symbol` must be non-empty and no longer than 32 characters.
  - `Price` must be > 0.

By keeping validation in a separate validator class, we can:

- Unit test validation rules independently.
- Keep controllers and handlers focused on orchestration and mapping, not on low-level validation details.

### Q: Why is there no query validator right now?

A: The query (`GetSymbolStatisticsQuery`) is very simple and only has one string property (`Symbol`). In a real-world system, we might validate:

- That the symbol is in a certain allowed format.
- That it is within a whitelist for a given tenant.

For this demo, the query handler simply returns `null` and the controller returns `404` if no data exists for that symbol. Adding a validator later is straightforward if needed.

### Q: How does the API handle not found cases for symbol statistics?

A: `GetSymbolStatisticsQueryHandler` returns `null` when the repository does not find statistics for the symbol. The controller checks this and returns:

- `404 Not Found` if the DTO is `null`.
- `200 OK` with the DTO otherwise.

This is a standard REST pattern: non-existing resources are represented as 404.

---

## 4. Domain Model & Entities

### Q: What are the key domain entities and what do they represent?

A:

- `PriceUpdate`  
  A single price observation for a symbol, with a timestamp.

- `SymbolStatistics`  
  Aggregated statistics for a symbol, including:
  - `CurrentPrice`
  - `MovingAverage`
  - `UpdateCount`
  - `LastUpdateTime`
  - `MinPrice`
  - `MaxPrice`

- `PriceAnomaly`  
  Represents a detected spike:
  - `ReferencePrice` (min or max in the window)
  - `NewPrice`
  - `ChangePercent`
  - `Timestamp`
  - `Direction` (Up or Down)

`SymbolStatistics` also has a `Clone()` method so we can return snapshots to callers without exposing internal state for mutation.

### Q: Why is `PriceAnomaly` a domain entity, even though you are currently just logging it?

A: Because anomaly detection is part of the **business logic** of this service:

- The concept of an anomaly (reference price, new price, change percent, direction, timestamp) belongs to the domain.
- Even though the current implementation logs anomalies, in a more complete system we might:
  - Store them in a database.
  - Publish them to Kafka for downstream consumers.
  - Attach them to alerting or risk management services.

Keeping `PriceAnomaly` in the domain layer makes it easier to evolve the system in that direction.

### Q: Why is `SymbolStatistics` mutable but returned as a clone?

A:

- Internally, `SymbolStatistics` is mutable so that the worker can update it efficiently (no need to allocate a new object on every tick).
- Externally, we expose a **clone** so that:
  - Consumers cannot accidentally modify internal state.
  - We preserve encapsulation and thread-safety.

This pattern is common in high-performance systems where internal state changes frequently but you want to safely expose snapshots.

### Q: Did you consider making `SymbolStatistics` immutable?

A: Yes, but immutable objects would require creating a new instance for every update. For a high-frequency stream (thousands of updates per second), this would:

- Add GC pressure.
- Increase allocations significantly.
- Potentially degrade throughput.

Using a mutable object with careful encapsulation (via `Clone()`) gives us a good balance between performance and safety.

---

## 5. Concurrency & High Throughput

### Q: How does this system achieve high throughput for processing price updates?

A: Through a combination of:

1. **Channel-based pipeline** using `Channel<PriceUpdate>`:
   - Non-blocking, async-friendly producer/consumer model.
2. **Partitioning**:
   - Symbol space is partitioned across multiple workers.
   - Each worker handles a subset of symbols.
3. **Single-writer pattern**:
   - Each partition worker is the only writer for the symbol states in that partition.
   - Greatly reduces contention and the need for locks.
4. **O(1) data structures**:
   - `MovingAverageBuffer` and `SlidingWindow` provide O(1) updates with constant memory.

These choices together allow the system to handle 10k+ ticks per second comfortably on a modern machine.

### Q: Why did you use `Channel<T>` instead of `BlockingCollection` or a plain `ConcurrentQueue`?

A: `Channel<T>` is a more modern and flexible abstraction:

- Native `async/await` support for both producers and consumers.
- Supports **bounded** channels with configurable behavior (`DropOldest` in our case).
- Designed for high-throughput, streaming scenarios.

While `BlockingCollection` and `ConcurrentQueue` are useful, `Channel<T>` fits better when we want:

- Backpressure semantics.
- Asynchronous non-blocking reads/writes.
- Cleaner integration with `IHostedService` and background tasks.

### Q: How does partitioning work exactly?

A:

- We configure a number of partitions `P` via `MarketDataProcessingOptions.Partitions`.
  - If `Partitions = 0`, we default to `Environment.ProcessorCount`.
- For each partition `i`:
  - We create a bounded `Channel<PriceUpdate>`.
  - We start one worker task that reads from that channel.
- For each `PriceUpdate`, we compute:

```csharp
int hash = symbol.GetHashCode() & 0x7fffffff;
int partitionId = hash % partitionCount;
```

This ensures that **all updates for a given symbol** always go to the same partition and the same worker.

### Q: Why did you choose this hash-based partitioning strategy and not something like range-based partitioning?

A: Hash-based partitioning is:

- Simple to implement.
- Provides an even distribution of symbols across partitions in most real-world scenarios.
- Does not require managing symbol ranges or rebalancing when adding/removing partitions.

Range-based partitioning could be useful if we had specific ordering or affinity requirements, but for this system, hash-based partitioning is sufficient and easier to maintain.

### Q: Why did you choose `DropOldest` for the `FullMode` of the channel?

A: This is a deliberate **backpressure policy**:

- When a partition is temporarily overwhelmed, the channel will not grow infinitely.
- Instead, the oldest items in that partition are dropped to make room for newer ones.
- For real-time streams like market data, older updates can often be sacrificed when extreme overload occurs; what matters most is current state and recent moves.

In a different context, we might choose:

- `DropNewest` (keep all old items, drop new ones).
- `Wait` (block producers until space is available).

But here, `DropOldest` provides a reasonable trade-off between safety and freshness.

### Q: How do you avoid race conditions in symbol state?

A: The single-writer per partition rule is crucial:

- All updates for a symbol are routed to the worker responsible for that partition.
- Only that worker ever mutates the `SymbolState` for that symbol.
- No other threads write to that state, so we do not need locks or complex synchronization.

Global counters are updated with `Interlocked`, which is a standard pattern for simple cross-thread aggregation.

Snapshots of `SymbolStatistics` are returned via `Clone()` to avoid external mutation of internal state.

### Q: Why did you pick `ConcurrentDictionary` for `PartitionState.Symbols` instead of `Dictionary`?

A: In the current design, only the worker for that partition writes to `PartitionState.Symbols`. However:

- Using `ConcurrentDictionary` gives extra safety if the code evolves and other threads start to read or write state.
- It avoids the risk of accidentally introducing race conditions if a future developer accesses it incorrectly.

If we wanted maximum performance and were confident in strictly maintaining single-writer semantics, we could switch to `Dictionary<string, SymbolState>` for an additional micro-optimization.

---

## 6. Data Structures: Moving Average & Sliding Window

### Q: How is the moving average implemented, and why is it efficient?

A: The moving average is computed with a **fixed-size ring buffer**:

- Array-backed (`double[] _buffer`) with a fixed capacity `N`.
- We maintain:
  - `_sum`: sum of all values in the buffer.
  - `_index`: index of where to write the next value.
  - `_count`: how many values have been seen (≤ N).

When adding a new value:

1. If `_count < N`, increase `_count` and add the value to `_sum` and `_buffer[_index]`.
2. If `_count == N`, subtract the oldest value (at `_buffer[_index]`) from `_sum`, overwrite it with the new value, and add it to `_sum`.

The average is `_sum / _count`.

Advantages:

- O(1) per update.
- Constant memory usage.
- No allocations after construction.

This is ideal for high-frequency updates.

### Q: How is the 1-second sliding window implemented for anomaly detection?

A: Using **two monotonic deques** inside `SlidingWindow`:

- **Monotonic deque**: a double-ended queue that keeps elements in sorted order (either non-decreasing or non-increasing price), but only over the **current window**.

We maintain:

- `_minDeque`:
  - Non-decreasing prices.
  - Head element is the minimum in the current window.
- `_maxDeque`:
  - Non-increasing prices.
  - Head element is the maximum in the current window.

For a new sample `(timestampMs, price)`:

1. **Evict old entries**:
   - Remove elements from the head while their timestamp is `< timestampMs - windowMs`.
2. **Maintain monotonic order**:
   - For `_minDeque`:
     - While the last element has a price > new price, pop it from the tail.
   - For `_maxDeque`:
     - While the last element has a price < new price, pop it from the tail.
3. Append the new element at the tail of each deque.

To get `min` and `max` for the current window, we peek the head of `_minDeque` and `_maxDeque`.

This approach is:

- O(1) amortized per tick.
- Allocation-free in steady state.
- Suitable for real-time detection.

### Q: Why is the sliding window based on timestamps (ms) instead of count-based?

A: Because the requirement is defined in terms of **time**, not count: “spikes greater than 2% within any 1-second interval”.

If we used purely count-based windows, we would not consistently capture “within 1 second” semantics.
Using timestamps and a sliding time-based window aligns directly with the business rule.

### Q: How do you handle clock issues or out-of-order events in the sliding window?

A: In this demo implementation, we assume:

- The timestamps are **monotonic enough** for each symbol.
- Events are **mostly in order**.

If out-of-order events became common, we could:

- Reject events that are older than the current cutoff.
- Or accept them but note that they might not affect the sliding window as expected.

For a production system, we might add sequence numbers or use more robust event-time processing, possibly inspired by stream processing techniques (e.g., watermarks). For this assignment, I kept the implementation simple and focused.

---

## 7. Anomaly Detection Logic

### Q: Can you walk through the anomaly detection logic step by step for a new tick?

A: For each `PriceUpdate` processed by a worker:

1. Convert `timestamp` to milliseconds (`timestampMs`).
2. Convert `price` to a `double` for analytics.
3. Call `SlidingWindow.AddSample(timestampMs, price)`.
4. Call `SlidingWindow.TryGetMinMax(timestampMs, out min, out max)`.
   - If it returns false, the window is empty: no anomaly to detect.
5. Compute `threshold = AnomalyThresholdPercent / 100m`.

Then:

- **Upward spike** check:
  - If `min > 0`:
    - `changeUp = (price - min) / min` (decimal)
    - If `changeUp > threshold`, create a `PriceAnomaly` (reference = `min`, direction = Up) and pass it to `IAnomalySink`.
- **Downward spike** check:
  - If `max > 0`:
    - `changeDown = (price - max) / max`
    - If `changeDown < -threshold`, create a `PriceAnomaly` (reference = `max`, direction = Down) and pass it to `IAnomalySink`.

Counters for anomalies are incremented via `Interlocked.Increment` for global metrics.

### Q: How sensitive is this anomaly detection, and how would you tune it?

A: Sensitivity is controlled by:

- `AnomalyThresholdPercent` – sets the minimum percentage change to consider a spike.
- `SlidingWindowMilliseconds` – sets the time interval within which changes are evaluated.

To tune it:

- Lower the threshold to detect smaller changes (more anomalies).
- Increase the threshold to reduce the noise (fewer anomalies).
- Adjust the window length to capture shorter or longer-term movements.

### Q: How would you change the anomaly detection logic to support more advanced scenarios?

A: Some options:

1. **Configurable window length per symbol**:
   - Extend `MarketDataProcessingOptions` or have a symbol-specific configuration store.
2. **Multiple thresholds** (warning vs critical):
   - Add `WarningThresholdPercent` and `CriticalThresholdPercent` and classify anomalies accordingly.
3. **Rate-based anomalies**:
   - Detect spikes in rate of change, not just price differences.
4. **Pattern-based anomalies**:
   - Use the window as an input to ML models (e.g., anomaly detection libraries or custom models).
5. **Context-aware anomalies**:
   - Take into account time of day, symbol volatility, or other contextual factors.

The current design isolates the anomaly detection logic so that these enhancements can be plugged in without rewriting the entire pipeline.

---

## 8. Configuration, Options, and Deployment

### Q: How is the processor configured and how does that affect behavior?

A: Via `MarketDataProcessingOptions`, bound from the `MarketDataProcessing` section in `appsettings.json`:

- `Partitions`: number of partitions (workers).
- `ChannelCapacity`: per-partition channel capacity.
- `MovingAverageWindow`: number of samples for moving average.
- `AnomalyThresholdPercent`: ±% threshold for spikes.
- `SlidingWindowMilliseconds`: window length for spikes.

Changing these values does not require code changes; just update configuration and restart the service.

### Q: Why did you put configuration in `appsettings.json` and use `IOptions<T>`?

A: Because this is the **idiomatic way** in modern ASP.NET Core / .NET applications:

- Configuration is externalized (file/env/KeyVault/etc.) and not hardcoded.
- `IOptions<T>` binds strongly-typed options classes, which are:
  - Easier to use than raw string dictionaries.
  - Safer to refactor with static typing.

This pattern scales well to multi-environment setups and secrets stores.

### Q: How would you deploy this system in a production environment?

A: Typical approach:

1. **Containerization**:
   - Add a Dockerfile for `MarketData.API` (multi-stage build with `dotnet publish`).

2. **Orchestration**:
   - Deploy to Kubernetes (AKS, EKS, or GKE).
   - Use ConfigMaps/Secrets for configuration (e.g., `MarketDataProcessing`). 

3. **Scaling**:
   - Horizontal Pod Autoscaler (scale-out) when CPU or request latency increases.
   - Within each pod, the processor already uses partitioning and multiple workers to exploit all CPU cores.

4. **Monitoring**:
   - Integrate OpenTelemetry / Prometheus / Grafana to observe metrics like processed ticks/sec, anomalies/sec, API latency.

Although the repo does not include all of these steps, the architecture is designed so these can be added incrementally.

### Q: Would you expose metrics through an HTTP endpoint?

A: Yes, in a production system I would expose metrics via an endpoint compatible with Prometheus:

- Expose counters for `TotalProcessedTicks`, `AnomaliesDetected`, average latency per partition, etc.
- Possibly expose per-symbol statistics for a subset of “hot” symbols.

Currently, the system exposes these metrics through the `ProcessingStatistics` DTO, which can be integrated into a metrics endpoint or a health check later.

---

## 9. Testing and Quality

### Q: If you were to add automated tests, what would you focus on?

A:

1. **Domain tests**:
   - `SymbolStatistics.ApplyUpdate` (correct min/max/moving average updates).
   - `PriceAnomaly` initialization and direction logic.

2. **Analytics tests**:
   - `MovingAverageBuffer` with known numeric sequences.
   - `SlidingWindow` with synthetic timestamps to verify min/max and eviction behavior.

3. **Application tests**:
   - Command handlers and query handlers using mocked `IMarketDataProcessor` and `IStatisticsRepository`.

4. **Integration tests**:
   - Use `WebApplicationFactory` to spin up API in-memory.
   - Send HTTP requests and verify responses, including 404 cases.

5. **Load tests** (if time allows):
   - Use a tool like NBomber or K6 to simulate high tick rates and validate throughput/latency.

### Q: How do you ensure the system is debuggable in a live interview?

A:

- The code uses clear, small, composable methods (e.g., `EnqueueUpdateAsync`, `WorkerLoopAsync`).
- There are natural breakpoints you can set in Visual Studio/Rider:
  - Command handler, processor, worker loop, query handler.
- Data structures are small and inspectable:
  - You can inspect `SymbolState.MovingAverage`, `SymbolState.Window`, `SymbolState.Statistics`.
- Logs (or console outputs for anomalies) provide a rough view of what is happening.

Together with the diagrams and Q&As in the docs, you have a mental map for stepping through code live.

### Q: Would you use mocks or fakes for testing the processor itself?

A: For the processor, I would likely write **integration-style tests** with:

- A real `HighPerformanceMarketDataProcessorService` instance.
- A test harness that:
  - Starts the processor.
  - Enqueues synthetic `PriceUpdate`s.
  - Waits briefly for processing.
  - Reads statistics and asserts on expected values.

This tests the concurrent pipeline more realistically than heavy mocking. For Application-layer tests, mocking `IMarketDataProcessor` is appropriate.

---

## 10. Security Considerations

### Q: Are there any security aspects you would think about for this API in production?

A:

- **Authentication/Authorization**:
  - Protect POST endpoints so only trusted services can send data.
  - Restrict GET endpoints if data is sensitive.
- **Input validation**:
  - Already using FluentValidation to validate fields.
  - Could add length and format rules for symbols, and business rules for accepted price ranges.
- **Rate limiting / throttling**:
  - Limit how many requests clients can send per time unit.
- **Transport security**:
  - Enforce HTTPS/TLS.
- **Logging hygiene**:
  - Avoid logging sensitive identifiers if present in future extensions.

For this coding assignment, I intentionally did not implement full auth/rate limiting to keep the focus on architecture and processing.

### Q: Would this system be multi-tenant in a real SaaS context? How would you handle that?

A: If multi-tenancy were required, options include:

- Add a `TenantId` to `PriceUpdate` and `SymbolStatistics`.
- Partition by `(TenantId, Symbol)` instead of just `Symbol`.
- Provide tenant-aware APIs and restrict access via authorization policies.

The current design can accommodate this without major structural changes.

---

## 11. Alternatives and Trade-offs

### Q: Why not use a database for statistics instead of in-memory storage?

A: For this use case:

- The focus is **real-time processing and low latency**.
- In-memory storage is much faster and sufficient for demonstration.
- Persisting every tick or per-symbol update to a DB would add IO latency and complexity.

However, the design includes `IStatisticsRepository`, so adding a DB-backed implementation is straightforward. We could:

- Persist periodic snapshots of `SymbolStatistics`.
- Persist anomalies for later analysis.
- Use a cache (like Redis) for hot symbols.

### Q: Could you have used a reactive framework like TPL Dataflow or Rx?

A: Yes, it’s possible:

- **TPL Dataflow** could model the pipeline with blocks and links.
- **Reactive Extensions (Rx)** could represent the price stream as observables.

I chose `Channel<T>` because:

- It’s part of the core BCL, no additional dependency.
- It gives fine-grained control over backpressure.
- It integrates nicely with `async/await` and `IHostedService`.

### Q: Why is the processing logic in `HighPerformanceMarketDataProcessorService` and not in the Domain layer?

A: Because the processing logic is tightly coupled to:

- Concurrency strategy (partitioning, channels).
- Resource management (threads, cancellation tokens).
- Backpressure policy.

These aspects are infrastructure concerns, not pure domain concerns. The domain still owns definitions like `PriceUpdate`, `SymbolStatistics`, and `PriceAnomaly`, but the mechanics of how we process streams belong in Infrastructure.

---

## 12. Future Work and Extensions

### Q: If you had more time, what features would you add?

A:

1. **Real feed integration**:
   - Implement an adapter that consumes data from a live exchange via WebSockets.
   - Use `IMarketDataProcessor` as the ingestion point.

2. **Historical anomaly storage**:
   - Persist `PriceAnomaly` in a database or data lake.
   - Provide an API to query anomaly history per symbol.

3. **Extended API surface**:
   - `GET /api/prices` → list all symbols with basic stats.
   - `GET /api/prices/{symbol}/anomalies` → list recent anomalies.

4. **Observability & metrics**:
   - Integrate OpenTelemetry.
   - Expose metrics endpoints for Prometheus.

5. **Testing & benchmarks**:
   - Add NBomber or a similar tool to benchmark throughput and latency.

### Q: Could this design scale horizontally across multiple instances?

A: Yes, but it requires sharding symbols across instances:

- For example, use an external router that decides which instance handles which subset of symbols.
- Alternatively, use a message bus where each instance subscribes to specific symbol ranges or partitions.

The in-process partitioning (threads & channels) would then be a **second level** of partitioning inside each instance.

### Q: What would you change first if you discovered bottlenecks in performance?

A: I would:

1. Profile the system under load to see where time is spent (e.g., allocations, contention).
2. If the processor is the bottleneck:
   - Tune `Partitions` to match CPU cores.
   - Check if `ConcurrentDictionary` can be swapped with `Dictionary` safely.
   - Reduce logging verbosity in hot paths.
3. If API layer is the bottleneck:
   - Add caching for read endpoints.
   - Consider batch endpoints or streaming.

---

You can use these Q&A sections as a mental script during interviews:  
pick the relevant questions, adapt the wording to your own style, and you’ll be able to explain **both the “what” and the “why”** behind this system at a senior level.


---

## 13. Additional Interview Scenarios (Advanced Q&A)

### Q: How would you adapt this design if you suddenly needed exactly-once processing guarantees per tick?

A: Exactly-once semantics are expensive but can be approximated by:

- Assigning a **unique ID** or sequence number per tick.
- Introducing an **idempotency layer** (e.g., a small store of recently processed IDs per symbol).
- Before processing a tick, check whether we have already seen its ID; if yes, skip.
- For persistence, use a transactional store where both state update and "seen ID" record are written atomically.

In this assignment, we focus on **at-least once** semantics in memory, but the abstractions
(IMarketDataProcessor, repositories) allow us to plug in a more robust store if needed.

### Q: How do you reason about failure modes of the worker loops?

A: Failure modes include:

- Unhandled exceptions inside a partition worker.
- Channel reader encountering an unexpected error.
- Cancellation tokens not respected.

We mitigate by:

- Wrapping the worker loop in a try/catch and logging errors.
- Using cooperative cancellation via `CancellationToken`.
- Designing the loop so that failures are **isolated to a partition**; other partitions continue.

In production, we might:

- Add automatic worker restarts.
- Emit metrics for worker crashes.
- Potentially isolate partitions in separate processes for even stronger fault isolation.

### Q: What patterns from this system would you reuse in another streaming application (e.g., telemetry ingestion)?

A: The following patterns are broadly reusable:

- Partitioned worker model over channels (hashing keys to partitions).
- Single-writer per key/partition.
- O(1) data structures for sliding analytics (moving average, sliding window).
- Clear separation of ingestion (commands) and query path (read model).

Only the domain entities (`PriceUpdate`, `PriceAnomaly`) and business rules change.

### Q: How can you defend the use of in-memory state in an interview where persistence is questioned?

A: I would explicitly call out:

- **Scope of the assignment**: focus on high-throughput ingestion and anomaly detection logic.
- The design **does not preclude** persistence:
  - Domain and Application layers are storage-agnostic.
  - Repositories are abstractions that can be re-implemented over a DB.
- If persistence is required, I would:
  - Introduce a DB-backed repository.
  - Possibly persist only **snapshots** and anomalies, not every tick.

This shows that I am aware of the trade-offs and that in-memory is a **deliberate simplification**, not a limitation of the architecture.

### Q: If an interviewer asks “What are you most unhappy about in this codebase?”, what would you answer?

A: A good, honest answer could be:

- “We currently store everything in memory, so we lack durability; I would like to add a persistence layer.”
- “Anomaly detection is simple; I would like to add more sophisticated, statistically grounded algorithms.”
- “We do not yet have a full automated test suite; I would like to add unit/integration/load tests as described in TESTING_STRATEGY.md.”

Owning these limitations and having a concrete improvement plan signals maturity rather than weakness.
