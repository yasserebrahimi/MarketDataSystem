# MarketDataSystem – Extended Interview Q&A

This document is written as if an interviewer is asking you questions about the system.  
Use it to **prepare** and become deeply comfortable with every aspect of the project.

Sections:

1. High-level / Product questions  
2. Architecture & layering  
3. Concurrency & performance  
4. Domain & data structures  
5. Simulation & ingestion  
6. Testing & quality  
7. Security & operations  
8. Trade-offs, limitations, and future work  
9. Coding & implementation details  
10. Behavioral / ownership style questions

---

## 1. High-Level / Product

### Q1: In one minute, what does this system do?

**A:** It is a real-time market data processing service. It consumes high-frequency price updates (ticks) for multiple symbols, keeps per-symbol statistics (like current price and moving average), detects short-term price spikes (>2% within any 1-second window), and exposes that state and anomaly information via a simple HTTP API. It also includes a built-in simulation feed so the whole system can be demonstrated and load-tested without external dependencies.

---

### Q2: Who is the “customer” of this service?

**A:** The direct customers are other backend services, dashboards, and trading tools that need real-time views of prices and anomalies. Indirectly, it could serve traders, risk managers, or monitoring dashboards that want to see volatility and anomaly patterns across instruments.

---

### Q3: What are the top three non-functional requirements you optimised for?

**A:**

1. **Throughput and latency** – ability to handle 10k+ ticks per second with constant-time operations per tick.
2. **Clarity and explainability** – architecture and code organised so that I can explain it in an interview and new engineers can onboard quickly.
3. **Extensibility** – clean layering and abstractions so we can later add persistence, sharding, and more advanced analytics without rewriting everything.

---

## 2. Architecture & Layering

### Q4: Why did you choose a multi-project solution instead of a single monolith project?

**A:** I wanted the structure to reflect the boundaries of the system:

- `API` is about transport & hosting (HTTP, controllers, DI).
- `Application` defines use cases: commands, queries, and interfaces.
- `Domain` holds core concepts (PriceUpdate, SymbolStatistics, PriceAnomaly).
- `Infrastructure` implements processing, repositories, simulation.

This separation helps in several ways:

- Different layers evolve at different speeds (e.g., API versioning vs. domain stability).
- It makes unit testing easier because Application and Domain are mostly free of framework dependencies.
- It shows that I can design a system that won’t turn into a “big ball of mud” as it grows.

---

### Q5: Can you explain how dependencies flow between these layers?

**A:**

- `MarketData.API` depends on `MarketData.Application` (for commands/queries/interfaces).
- `MarketData.Application` depends on `MarketData.Domain` and **only** on abstractions in `MarketData.Infrastructure` (interfaces).
- `MarketData.Infrastructure` depends on `MarketData.Domain` and `MarketData.Application` interfaces to implement repositories and processor services.
- `MarketData.Domain` is at the center, with no references outward.

In other words, dependencies point inward, which is consistent with Clean Architecture or Onion Architecture principles.

---

### Q6: Why did you use MediatR instead of calling services directly from controllers?

**A:**

- It standardises the way the API interacts with application logic: one command or query per use case.
- It keeps controllers very thin – they just translate HTTP to a command/query and delegate.
- It simplifies cross-cutting concerns: if later we add logging, metrics, or validation behaviors, we can plug them into the MediatR pipeline without touching every controller.
- It is a common pattern in modern .NET codebases, so many engineers expect it and understand it quickly.

That said, for a very small project direct service calls would also be fine – using MediatR here is about **clarity and extensibility**, not necessity.

---

### Q7: How would you explain the architecture to a junior developer in simple terms?

**A:**

1. The **API project** receives HTTP requests.
2. It forwards them to **application handlers** (commands/queries) using MediatR.
3. Handlers call into a **processor** and **repositories** via interfaces.
4. The **processor** is a background service that reads updates from queues, keeps per-symbol state, and detects anomalies.
5. The **repositories** give read access to that state for queries.
6. The **domain** layer defines the core concepts: price update, statistics, anomaly.

So: HTTP → handlers → processor / repositories → domain objects.

---

## 3. Concurrency & Performance

### Q8: How do you achieve high throughput while keeping the design simple?

**A:** I partition the workload and use constant-time data structures:

- There is an array of `Channel<PriceUpdate>` instances, one per partition.
- Each partition has a dedicated worker task that owns updates to symbols in that partition.
- Routing is done via `hash(symbol) % partitionCount`, so all updates for a symbol go to the same worker.
- Each worker updates in-memory state using O(1) operations: `MovingAverageBuffer` and `SlidingWindow`.

This approach scales with CPU cores and avoids heavy locks because each symbol is effectively single-threaded by design.

---

### Q9: Why did you choose `System.Threading.Channels` instead of `BlockingCollection` or TPL Dataflow?

**A:**

- `Channel<T>` is a modern, high-performance, asynchronous producer/consumer primitive in .NET.
- It integrates nicely with `async/await` for both producers and consumers.
- It supports bounded channels with different overflow policies (`DropOldest`, etc.), which is ideal for implementing backpressure.
- TPL Dataflow is more powerful but heavier; for this particular scenario I just need a simple, strongly-typed queue between producers and workers.

---

### Q10: How do you prevent unbounded memory growth when the incoming rate exceeds the processing rate?

**A:**

- Each partition channel is **bounded** with a configured capacity.
- The channel is configured with `FullMode = DropOldest`:
  - When the channel is full and a new item arrives, the **oldest** item in that channel is dropped.
  - This keeps memory bounded and favours newer data.
- This is consistent with the semantics of real-time market data consumers who typically care more about current prices than about delivering every single tick under overload conditions.

If the system required strict “no loss” guarantees, we would instead use durable queues and backpressure, but here we optimise for **freshness and stability**.

---

### Q11: How would you scale this system horizontally if one machine is not enough?

**A:**

- At the node level, we already have in-process sharding by partition.
- To scale out, we would add a **routing layer** in front of multiple nodes (or use a partitioned message bus like Kafka):
  - Partition by symbol space (e.g., consistent hashing).
  - Route all updates for a given symbol to the same node.
- Each node then runs the exact same partitioned processor internally.
- Read-side can be either:
  - Routed based on symbol (read from the node that owns it).
  - Or implemented over a shared persistent store that aggregates the results from all nodes.

The current interfaces (`IMarketDataProcessor`, `IStatisticsRepository`) are compatible with such future changes.

---

### Q12: What are the main sources of allocation/GC pressure and how did you minimise them?

**A:**

- The main potential allocation sources are:
  - Per-tick allocations in data structures.
  - Boxing / LINQ enumerations.
  - Logging in the hot path.
- To minimise them:
  - `MovingAverageBuffer` and `SlidingWindow` allocate their arrays/deques **once** per symbol when the symbol is first seen.
  - Per-tick operations are O(1) and do not allocate new arrays or lists.
  - I avoided LINQ and short-lived collections in the hot path.
  - I keep logging around the pipeline, but not per tick.

In real production code I would measure with a profiler (e.g., dotnet-trace, PerfView) and iterate further.

---

## 4. Domain & Data Structures

### Q13: Why do you use `decimal` in the domain and `double` inside analytics?

**A:**

- Financial values are usually best represented as `decimal` to avoid surprises around binary floating point and rounding.
- High-frequency numeric operations (like moving averages and sliding windows) are faster with `double` and can often tolerate small floating-point approximations.

So the approach is:

- Externally and in domain objects, prices are `decimal`.
- Inside the `MovingAverageBuffer` and `SlidingWindow`, they are converted to `double` for performance.
- When making decisions (like anomaly detection) or returning results, I convert back to `decimal` where needed.

This is a conscious trade-off between **numerical correctness** and **performance**.

---

### Q14: Explain how the moving average is calculated in detail.

**A:**

- Each `SymbolState` has a `MovingAverageBuffer` with a fixed capacity N (e.g., 64).
- When a new price arrives:
  1. If the buffer is not yet full, append it, add to the running sum, and increase the count.
  2. If the buffer is full, subtract the value at the current index from the sum, overwrite that slot, then add the new value to the sum.
  3. Move the index (wrapping around at capacity).
  4. The moving average is simply `sum / count` (where count ≤ N).

This makes each update O(1) with no list scans or re-allocations.

---

### Q15: How does the 1-second sliding window work for anomaly detection?

**A:**

- The `SlidingWindow` maintains two monotonic deques:
  - One for potential minimums (non-decreasing prices).
  - One for potential maximums (non-increasing prices).
- For each new sample `(timestampMs, price)`:
  1. Remove entries from the back of the min deque while their price is **greater** than the new price.
  2. Remove entries from the back of the max deque while their price is **less** than the new price.
  3. Append the new entry to both deques.
  4. Evict entries from the front of each deque if they are older than `nowMs - windowMs`.
- At any point:
  - The front of the min deque is the current minimum in the window.
  - The front of the max deque is the current maximum in the window.
- An anomaly is detected when the absolute percentage difference between the latest price and the min or max exceeds the configured threshold.

This gives strict 1-second semantics with O(1) amortised cost per tick.

---

### Q16: Why not use a more advanced anomaly detection algorithm?

**A:**

- The assignment’s requirement is specifically about **percentage spikes in a 1-second window**.
- Implementing a simple, deterministic rule is better for clarity, testability, and interview discussion.
- The design deliberately keeps the anomaly detection logic isolated so that:
  - We could later plug in more advanced algorithms (z-scores, EWMA, ML models) without restructuring the whole system.

In an interview, I would highlight that the design is **extensible** even if the current algorithm is intentionally simple.

---

## 5. Simulation & Ingestion

### Q17: Why did you implement a simulation service?

**A:**

- The brief required simulating market data.
- It makes the repository **self-contained**: the interviewer can run it locally and immediately see the system in action.
- It allows for basic load testing without external dependencies.
- It demonstrates that the ingestion pipeline is generic – simulation is just another producer of `PriceUpdate` events.

---

### Q18: How does the simulation service generate prices?

**A:**

- For each symbol, it keeps a `decimal` current price, initialised to a configured `InitialPrice`.
- At each interval (derived from `TicksPerSecond`):
  - It generates a random percentage `delta` in `[-MaxJitterPercent, +MaxJitterPercent]`.
  - It multiplies `currentPrice` by `(1 + delta)`.
  - It emits a `PriceUpdate` with the new price and current UTC timestamp.
- This “random walk” model is simple but enough to trigger moving averages and anomaly detection in interesting ways.

---

### Q19: How would you plug in a real market data feed instead of the simulation?

**A:**

- The ingestion interface is `IMarketDataProcessor.EnqueueUpdateAsync(PriceUpdate)`.
- A real feed adapter (e.g., WebSocket client, Kafka consumer) would:
  - Parse messages from the external source.
  - Map them into `PriceUpdate` instances.
  - Call `EnqueueUpdateAsync` for each tick.
- The rest of the system (processor, statistics, anomalies, API) does not care where the data came from.

This shows that the design separates **transport** from **processing logic**.

---

## 6. Testing & Quality

### Q20: How would you unit-test the analytics structures?

**A:**

- For `MovingAverageBuffer`:
  - Feed deterministic sequences (e.g., 1, 2, 3, 4 with capacity 3) and assert that the moving average matches expected values after each insertion.
  - Test edge cases like capacity 1, very large/small numbers, and repeated values.
- For `SlidingWindow`:
  - Use synthetic timestamps where you control the “clock” (e.g., 0, 200, 400, 1200 ms).
  - Assert that min/max reflect only the entries within the last `windowMs`.
  - Test addition and eviction boundaries carefully.

These tests are pure and do not depend on threading or async code.

---

### Q21: How do you test the processor plus repositories together?

**A:**

- Write an integration test that:
  1. Creates a processor instance with a small number of partitions and capacity.
  2. Starts the worker loops (simplified for test).
  3. Feeds a known sequence of `PriceUpdate` events.
  4. Waits briefly for processing to complete.
  5. Uses the repository to query statistics and anomalies.
  6. Asserts that the results match expectations (moving average, counts, anomalies).

This ensures that routing, per-symbol state, and repository projections all work together correctly.

---

### Q22: How would you do load testing?

**A:**

- Option 1: Use the internal simulation and tune `TicksPerSecond` and symbol count while monitoring `/api/metrics`.
- Option 2: Use an external tool (NBomber, K6, JMeter) to fire `POST /api/prices` at high rate.
- In both cases, I would track:
  - CPU and memory usage.
  - Worker queue sizes and throughput from `/api/metrics`.
  - Latency and error rate for HTTP endpoints.
- I would then tune:
  - `Partitions`, `ChannelCapacity`, and `SlidingWindowMilliseconds`.
  - Possibly the simulation parameters to generate realistic but challenging workloads.

---

## 7. Security & Operations

### Q23: What are the main security risks for this kind of service?

**A:**

- **Denial-of-service** – sending too many requests (especially `POST /api/prices`) to overload CPU or memory.
- **Data integrity** – malicious or malformed price updates (e.g., negative prices, insane values).
- **Exposure without authentication** – if endpoints are exposed publicly without auth, anyone can read or spam them.
- **Configuration mistakes** – running without HTTPS or with debug settings in production.

---

### Q24: How would you secure this service in a production environment?

**A:**

- Put it behind an API gateway that handles:
  - Authentication (JWT, OAuth2).
  - Authorization (role-based access to read/write endpoints).
  - Rate limiting and throttling.
- Enforce HTTPS and disable HTTP in production.
- Add structured security logging and integrate with SIEM for monitoring suspicious patterns.
- Validate all inputs strictly (e.g., enforce allowed symbol sets, price ranges).

The current code focuses more on architecture and performance, but it is compatible with these security measures.

---

### Q25: What operational metrics would you expose?

**A:**

- Processor metrics:
  - Total processed ticks.
  - Anomalies detected.
  - Active symbols count.
  - Queue size per partition (or aggregate).
  - Throughput per second over short windows.
- API metrics:
  - Request rate and latency per endpoint.
  - Error count (4xx/5xx) per endpoint.
- Resource metrics:
  - CPU usage, memory usage, GC pauses.

Currently I expose a simple `/api/metrics`; in production I would integrate with Prometheus/OpenTelemetry.

---

## 8. Trade-offs, Limitations, and Future Work

### Q26: What are you deliberately **not** doing in this version?

**A:**

- No persistence – all state is in memory; on restart we lose statistics and anomalies.
- No cross-node sharding – scaling is only inside a single process.
- Simple anomaly model – single threshold, single window.
- No formal auth/roles on the API surface.

These are conscious scope limitations to focus on core streaming and concurrency aspects.

---

### Q27: If you had three more days, what would you implement?

**A:**

1. **Persistence** of symbol statistics and anomalies to a database, plus migration scripts.
2. **More complete test suite** (unit + integration + load) and CI pipeline to run it on each commit.
3. **Better observability** – structured logging, metrics via OpenTelemetry, and a simple dashboard showing throughput and anomalies in real time.

---

### Q28: How would you adapt this system to a totally different domain (e.g., IoT sensor readings)?

**A:**

- Replace `PriceUpdate` with `SensorReading` (sensorId, value, timestamp).
- Replace anomaly rules with thresholds or models relevant to sensors (e.g., temperature spikes).
- Keep the same structure:
  - Ingestion via channels.
  - Per-key state in partitions.
  - Moving averages and sliding windows as analytics tools.
- Reuse MediatR-based API, repositories, and partitioning logic unchanged.

This shows that many of the ideas are domain-agnostic.

---

## 9. Coding & Implementation Details

### Q29: How do you handle cancellation and graceful shutdown of workers?

**A:**

- The processor is implemented as an `IHostedService` that starts worker tasks with a shared `CancellationToken`.
- On shutdown, the host signals cancellation, workers exit their loops, and we await task completion.
- Channels are completed to unblock pending reads.
- In an interview, I’d also mention ideas like “draining remaining items before shutdown” if we cared about completeness more than speed.

---

### Q30: What is your strategy for logging inside the processor?

**A:**

- I avoid per-tick logging to prevent log spam and overhead.
- I log at `Information` level at startup/shutdown and important configuration (partitions, capacities).
- I log at `Warning` or `Error` if a worker loop experiences an unexpected exception.
- In production I’d also log periodic summaries (e.g., ticks processed per partition every N seconds).

---

### Q31: How is configuration bound and validated?

**A:**

- I use a strongly-typed `MarketDataProcessingOptions` class.
- It is bound from configuration (`appsettings.json`, environment variables, etc.) using `IOptions` pattern.
- For critical values (e.g., `Partitions`, `ChannelCapacity`), I would add validation in startup to ensure they are positive and reasonable.
- This makes misconfigurations fail fast rather than causing subtle runtime problems.

---

## 10. Behavioral / Ownership

### Q32: If you joined a team and inherited this codebase, what would you do in your first week?

**A:**

1. Get the system running locally and in a staging environment.
2. Add or fix automated tests so I can change things safely.
3. Review performance and error logs to find the riskiest areas.
4. Talk to stakeholders to understand real SLAs and usage patterns.
5. Prioritise a small set of improvements (e.g., observability, error handling) and execute them end-to-end.

---

### Q33: What part of this project are you most proud of?

**A:**

- The balance between **performance awareness** (partitioning, O(1) structures) and **clean layering** (API/Application/Domain/Infrastructure).
- The amount of documentation and Q&A, which turns the project into a teaching artifact as well as a working system.

---

### Q34: What part are you least happy with?

**A:**

- Lack of persistence – losing all data on restart is not acceptable for many real-world use cases.
- Limited test coverage in code (even though the testing strategy is well-documented).
- Anomaly detection’s simplicity – I would like to add probabilistic or ML-based models in the future.

Acknowledging these weaknesses and having a clear improvement plan is part of senior-level ownership.
