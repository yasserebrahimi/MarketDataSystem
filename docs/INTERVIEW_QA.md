# Interview Q&A – Extended (Deep Dive)

> A very long-form Q&A to help you practice explaining **every** aspect of this project in an interview.

---

## 1) Architecture & Rationale

### Q: Why Clean Architecture + CQRS here?
**A:** Clear boundaries → testability, replaceable infrastructure, handlers as single use-cases. Commands mutate state (`ProcessPriceUpdate`), queries read (`GetSymbolStatistics`, `GetAllStatistics`, `GetRecentAnomalies`). This keeps controllers thin and the system easy to reason about.

### Q: What trade-offs did you make?
**A:** In-memory processing (fast) over durability (missing), simple anomaly rule (fast) over statistical sophistication, channels with `DropOldest` (stability) over strict lossless ingestion.

### Q: What would break first at very high load?
**A:** Partition backlogs (`queueSize`) grow. Mitigation: raise `Partitions`, scale out more instances, or reduce producer rate (simulation or gateway throttling).

---

## 2) Concurrency Model

### Q: Explain routing and single-writer semantics.
**A:** Partition = `(Channel<PriceUpdate>, Worker Task)`. Route symbol by `hash(symbol) % partitions`. A symbol always hits the same partition, so updates are serialized → no locking on per-symbol state.

### Q: Why channels over `BlockingCollection`/`ConcurrentQueue`?
**A:** Backpressure modes, async-friendly, non-blocking writes, better for high-throughput pipelines. `DropOldest` protects memory under overload.

### Q: Failure model of workers?
**A:** A worker crash should not bring down the host. We wrap loops with try/catch, log, and can restart. Partitions are isolated; others continue.

---

## 3) Data Structures & Complexity

### Q: MovingAverageBuffer in O(1) – how?
**A:** Fixed array (ring). Keep `sum`, `index`, `count`. On add: subtract outgoing value if full, add incoming, advance index. Average = `sum / count`.

### Q: SlidingWindow min/max in O(1) amortized – how?
**A:** Two **monotonic deques**. For min: keep non-decreasing prices; pop tail while new price is smaller. Evict head older than `now - windowMs`. Heads give min/max.

### Q: Precision: why `double` internally and `decimal` in domain?
**A:** Analytics prefer speed (`double`); domain/DTOs use `decimal` for financial clarity. Convert on boundaries.

---

## 4) Anomaly Detection

### Q: Formal rule?
**A:** Spike if `abs(new - ref) / ref * 100 >= threshold` using min/max inside the last 1 second. Direction = Up/Down.

### Q: Edge cases?
**A:** Very small prices (divide by near-zero) → clamp lower bound. Bursty arrivals all within 1ms → deques still work; timestamps are monotonic per tick. Clock skew from producers → prefer server timestamp if missing/invalid.

### Q: False positives?
**A:** With highly volatile series, more spikes; tune threshold, window length, or adopt z-score/EWMA later.

---

## 5) Performance & GC

### Q: Where do you avoid allocations?
**A:** Pre-size buffers, reuse SymbolState per symbol, avoid LINQ/closures on hot path, store numeric primitives, use struct-like DTOs if needed.

### Q: How to measure throughput?
**A:** `/api/metrics` counters. External: NBomber/K6 to push `POST /api/prices`. Track CPU, GC (Gen0/1/2) pauses, 99p latency.

### Q: When does `DropOldest` trigger?
**A:** If producers outrun consumers in a partition. We prefer freshness over historic completeness for a streaming analytics service.

---

## 6) Testing

### Q: What unit tests matter most?
**A:** `MovingAverageBuffer`, `SlidingWindow`, `SymbolStatistics` edge cases (min/max). Deterministic timestamps for sliding window eviction.

### Q: Integration test strategy?
**A:** Start processor with N partitions, push ticks, await small delay, then query repo and assert counts/MAs, optionally anomalies.

### Q: Load testing?
**A:** Use simulation or NBomber to hit `POST /api/prices` at 10–20k/s; watch `/api/metrics` and resource usage. Validate graceful degradation.

---

## 7) API & Validation

### Q: How do you validate inputs?
**A:** Model binding + validators: non-empty symbol (len ≤ 32), `price > 0`, optional ISO timestamp. Invalid → 400 with details.

### Q: Versioning?
**A:** Add ASP.NET API versioning if the surface evolves. For the assignment, single version is fine.

### Q: Why keep controllers thin?
**A:** Less coupling; business logic sits in handlers; easier unit testing and maintenance.

---

## 8) Security

### Q: Threats?
**A:** DoS on `POST /api/prices`, malformed payloads, unauthenticated access if public. Use gateway auth/zones, rate limiting, TLS termination, strict error handling.

### Q: What’s the plan for auth?
**A:** JWT/OAuth2 at gateway, roles for write/read separation. Internal services get client credentials; external users read-only.

### Q: Logging sensitive data?
**A:** No sensitive payloads. Log counts and summary only. PII not expected in this domain.

---

## 9) Operations

### Q: How to monitor health?
**A:** `/health` for liveness, `/api/metrics` for internal counters, logs for exceptions. Add OpenTelemetry + Prometheus later.

### Q: Playbook for growing queue?
**A:** Check CPU; increase partitions or scale out; reduce producer rate; raise capacity as a last resort; profile if still growing.

### Q: Config changes safely?
**A:** Centralized config store, immutable builds, rolling restarts, observability to validate impact.

---

## 10) Extensibility

### Q: Plugging a real feed (Kafka/WebSocket)?
**A:** Add a new hosted service producing `PriceUpdate`s to the same `IMarketDataProcessor` API. Processor remains unchanged.

### Q: Persistence strategy?
**A:** Introduce DB-backed read model; periodic snapshots; anomaly event stream. Keep interface contracts; swap implementations.

### Q: Multi-node sharding?
**A:** Consistent hashing gateway to route symbols to nodes; within node keep partitioning. Add node discovery/health and rebalancing.

---

## 11) .NET Internals & Pitfalls

### Q: Why `Channel<T>` over queues?
**A:** Async writers/readers, backpressure modes, zero/low allocation when bounded, high perf. `ConcurrentQueue` needs manual signaling and has no backpressure.

### Q: Async pitfalls?
**A:** Avoid `async void`, always pass `CancellationToken`, beware of sync-over-async deadlocks, no `Task.Result` in request path.

### Q: Thread-safety?
**A:** Single-writer per symbol removes locks. Shared structures (recent anomalies) use thread-safe collections with bounded size.

---

## 12) Failure Scenarios (Drills)

- Worker throws → log, restart task, keep other partitions alive.
- Channel full → DropOldest; alert via metric; scale up.
- Simulation misconfigured (TicksPerSecond too high) → tune down or disable.
- Hot symbol skew (all updates for one symbol) → increase partitions does not help; shard that symbol per key-suffix or dedicated worker.

---

## 13) Common Interview Traps & Good Answers

- **“Why not unbounded channels?”** → Memory/GC pressure, tail latencies explode, potential OOM.
- **“Why not DB first?”** → Assignment scope + latency/throughput targets; design is DB-ready via repos.
- **“How do you prove O(1)?”** → Walk through ring buffer and monotonic deque operations step-by-step.
- **“What if accuracy beats freshness?”** → Switch FullMode to Wait, increase capacities, or add buffering in gateway; different product goal.

---

## 14) Personal Reflection (what I’d improve next)

- Persistence for statistics/anomalies.
- OpenTelemetry tracing + proper metrics.
- Smarter anomaly models (EWMA, volatility-aware thresholds).
- Full automated test suite and perf CI jobs.


---

## 15) Advanced Architecture & Alternatives

### Q: How would you redesign this system if you were forced to use a single monolith without layers?
**A:** I would still try to preserve logical boundaries by using folders/namespaces instead of physical projects: `Api`, `Application`, `Domain`, `Infrastructure`. The code would live in one assembly but I would keep dependencies one‑way (Api → Application → Domain; Application → Infrastructure) and enforce these rules with conventions and code reviews. This would preserve most of the testability and clarity benefits of the current design.

### Q: Why not just have controllers talk directly to the processor service with no Application layer?
**A:** That would couple HTTP concerns to business logic. The Application layer decouples those concerns so that the processing engine could later be reused by other front‑ends (gRPC, message bus consumers, console tools) without duplicating controller logic. It also centralises validation and mapping so controllers remain very thin.

### Q: When would you *not* use CQRS in a system like this?
**A:** If the project were extremely small, with only one or two endpoints and no expectation of growth, CQRS might be overkill. A simple service class behind the controllers would be enough. CQRS becomes valuable when the number of use cases grows, when read and write scaling start to diverge, or when you want a clear testable unit per use case.

### Q: Could this system be implemented using an Actor model instead of channels and workers?
**A:** Yes. Each symbol or partition could be represented as an actor that receives messages (`PriceUpdate`, `GetStatistics`, etc.). The actor runtime would guarantee single‑threaded access to its state. The current design already borrows the same idea (single writer per symbol) but uses `Channel<T>` and tasks instead of a full actor framework. Using Akka.NET or Orleans would bring features like distribution and persistence but also additional complexity.

### Q: How would you argue for this architecture to a more traditional team that prefers big service classes?
**A:** I would emphasise the benefits in onboarding and change‑safety: each handler is a small, focused class; a new developer can read one handler and understand one use case. The architecture also makes automated testing easier, which reduces regression risk when requirements change. Finally, the processing engine is clearly isolated, so performance work does not leak into controller code.

### Q: What is the downside of having many small handlers and projects?
**A:** The main cost is indirection: it can take more clicks to see where something happens, and there is more boilerplate (e.g., mapping between DTOs and domain types). Build times can also grow with many projects, although here the solution is still small. The upside is long‑term maintainability and the ability to change infrastructure without touching business logic.

### Q: How does this architecture support adding persistence later?
**A:** Repositories are already abstracted behind interfaces. To add a database, we would implement `IStatisticsRepository` and `IAnomalyRepository` using EF Core or Dapper, register them in DI for production, and keep the in‑memory versions for tests or simple demos. Application and Domain code would not need to change, which is a strong sign the boundaries are well‑placed.

### Q: Would you ever split this single service into multiple microservices?
**A:** Only with clear reasons: for example, if anomaly detection grew into a complex ML‑based subsystem owned by a different team, or if statistics queries needed independent scaling and caching. Until then, a single well‑structured service is easier to operate and reason about than multiple microservices with cross‑service calls and data duplication.

### Q: How is this architecture different from a classic 3‑tier (UI/BL/DAL) design?
**A:** It is similar in spirit but more explicit. Clean Architecture emphasises *direction of dependencies* and treats Domain as the centre that does not depend on outer layers. Instead of a generic “BL”, we have concrete use cases (handlers) and domain entities. Infrastructure is clearly the outermost layer and can be swapped without changing the core logic.

### Q: How would you visualise the data flow to a non‑technical stakeholder?
**A:** I would use a simplified diagram with three boxes: “Feed Producers → Processing Engine → APIs/Dashboards”. Then I would explain in plain language that producers push prices in, the engine continuously keeps per‑symbol statistics and spots suspicious jumps, and APIs let dashboards and other systems retrieve both current stats and detected anomalies in real time.

---

## 16) Advanced Concurrency & Threading

### Q: How many threads does this system typically use at runtime?
**A:** There is one thread pool for the ASP.NET Core host, plus one logical worker task per partition and one task for the simulation service (if enabled). The exact number of OS threads is managed by the .NET thread pool, but conceptually you can think of “N workers + 1 simulation + K request handlers”, where N is the number of partitions.

### Q: What happens if `Partitions` is set to a very large number, like 128, on a 4‑core machine?
**A:** We will spawn 128 worker tasks. The thread pool will multiplex them over 4 cores. Context switching overhead grows, and each partition will receive fewer updates, so CPU cache locality might decrease. In practice, there is a sweet spot around the number of cores or slightly above; beyond that, throughput gains usually diminish or even reverse.

### Q: Why do you avoid locking inside the hot path?
**A:** Locks serialize threads and can introduce contention under high load. Because we guarantee a single writer per partition, we can update `SymbolState` without locking. Shared structures that *do* need thread safety (like the anomaly queue) are updated in a simple, bounded way where the small cost of locking or concurrent collections is acceptable.

### Q: How do cancellation tokens play into concurrency here?
**A:** Cancellation tokens are used to stop worker loops and the simulation service gracefully when the host is shutting down. They prevent tasks from running forever and allow us to break out of `while (!ct.IsCancellationRequested)` loops cleanly. This is important for orderly shutdowns and integration tests.

### Q: What are potential deadlock risks in this system?
**A:** The main risk would be blocking on async code (e.g., calling `.Result` on a task), which we deliberately avoid. Another risk would be designing circular waits between partitions or channels, but here flow is one‑way (API → processor → repositories), so we do not have inter‑partition dependencies that could deadlock.

### Q: How do you ensure that the processor keeps up with multiple producers (API + simulation)?
**A:** Both producers enqueue into the same partition channels. The bounded channels introduce backpressure if producers are too fast. We can observe queue sizes via metrics and adjust: disable simulation in production, tune `TicksPerSecond`, or scale out the service.

### Q: What is the impact of using `async` all the way down the stack?
**A:** It allows I/O‑bound work (like real DB calls in the future) to release threads while waiting, increasing scalability of the API under high concurrency. In the current in‑memory version, most work is CPU‑bound, so `async` is mainly used to keep the API consistent and ready for future persistence or external calls.

### Q: How would you debug a race condition if you suspected one?
**A:** I would first narrow the problem down to a single symbol or partition and add targeted logging with correlation IDs. Then I would inspect shared state for unsynchronised writes, and possibly create a stress test that hits the suspected code paths with many concurrent tasks. If needed, I would temporarily introduce extra assertions or `Interlocked` counters to detect unsafe access patterns.

### Q: Could using `Parallel.ForEach` instead of workers improve throughput?
**A:** Not in this model. `Parallel.ForEach` is great for batch processing, but here we have a continuous stream where order matters per symbol. Dedicated workers per partition simplify reasoning, maintain ordering guarantees per symbol, and allow us to maintain state in memory between ticks.

### Q: How would you convince someone that this concurrency design is safe?
**A:** I would show that each `SymbolState` is only ever mutated by the single worker of its partition. No two tasks write to the same state concurrently. Shared structures are either read‑only after construction or use thread‑safe collections. Then I would walk through a concrete example tick and demonstrate that there are no interleaving writes on the same data.

---

## 17) Persistence & Database Design

### Q: If you added a database for statistics, what schema would you start with?
**A:** A simple starting schema could be `Symbols(SymbolId PK, SymbolName)`, `SymbolStatisticsHistory(SymbolId FK, Timestamp, Price, MovingAverage, MinPrice, MaxPrice, UpdateCount)` and `PriceAnomalies(Id PK, SymbolId FK, Timestamp, Direction, MagnitudePercent, RawPrice)`. We could then introduce summarisation tables if queries require aggregation over time windows.

### Q: Would you persist every tick or only snapshots?
**A:** Persisting every tick is expensive and often unnecessary. For this project I would consider snapshotting periodic statistics (e.g., every second or every N ticks) and always persisting anomalies. If long‑term tick history is required, I might use a specialised time‑series store or data lake instead of the same OLTP database.

### Q: How would you keep DB writes from becoming a bottleneck?
**A:** Batch inserts, asynchronous write‑behind, and buffering are common techniques. The processor could publish events to a background persister that groups inserts. Using an append‑only table with minimal indexes also helps. In high‑end systems, you might dedicate a separate persistence service or use a message broker to decouple ingestion from durable storage.

### Q: How do you model idempotency in persistence?
**A:** Each tick can carry a unique sequence number or GUID. The persistence layer records the last processed sequence per symbol; if a duplicate arrives, it is ignored. For anomalies, we can combine symbol + timestamp + direction as a natural key to avoid double‑inserts of the same event.

### Q: Would you store raw anomalies or derived alerts?
**A:** I would store raw anomaly events in a normalised table. Higher‑level alerts (e.g., “5 spikes in 10 seconds”) can be derived by downstream systems. Storing raw events preserves maximum flexibility for future analytics without coupling the storage model to current alerting rules.

### Q: How would you design queries for a dashboard that shows “top 10 most volatile symbols today”?
**A:** We could either compute volatility offline and store it in a summary table, or compute it on the fly using statistics from the history table. A practical approach is to maintain a periodic summary (e.g., per minute) with standard deviation or average spike frequency, then query those summaries to find the top 10.

### Q: How do you handle schema evolution safely?
**A:** Use migrations with backward‑compatible changes (add columns, not remove/change types first), version API contracts, and keep old columns populated until all consumers are updated. For large datasets, use online migration strategies and feature flags to gradually roll out the new schema usage.

### Q: What kind of database would you start with for this system?
**A:** A relational database like PostgreSQL or SQL Server is a good default for structured statistics and anomalies. For extreme tick volumes, we might complement it with a time‑series DB or Kafka + object storage for raw tick archives. Starting simple lets us ship quickly and evolve as usage grows.

### Q: How would you test the DB integration end‑to‑end?
**A:** Use an integration test project that spins up a test database (or container), runs migrations, starts the API, pushes synthetic ticks, then queries both the API and the DB directly to ensure the stored data matches expectations. Clean up the database after each test run to keep tests reproducible.

### Q: How do you prevent the DB from becoming the throughput bottleneck?
**A:** Limit synchronous work on the hot path. The processor itself should update in‑memory state; persistence can lag slightly via a background writer. Use async I/O, connection pooling, minimal indexes, and partitioning or sharding if the dataset grows. Monitor DB metrics (locks, slow queries, I/O) and scale up or out as needed.

---

## 18) Kafka / Message Broker Integration

### Q: How would you integrate Kafka as an inbound source of price updates?
**A:** I would create a hosted service that runs a Kafka consumer, subscribes to one or more topics carrying `PriceUpdate` messages, deserialises them into the domain type, and forwards them to `IMarketDataProcessor.EnqueueUpdateAsync`. From the processor’s perspective, Kafka is just another producer, like the simulation or HTTP API.

### Q: How do you ensure ordering when consuming from Kafka partitions?
**A:** Kafka already guarantees ordering per partition. By choosing a Kafka partition key based on the symbol, all messages for a symbol will arrive in order. This matches our internal partitioning strategy: we can assign each Kafka partition to one internal processor partition or keep a 1:1 mapping for simplicity.

### Q: Would you still keep HTTP ingestion if Kafka is used?
**A:** Yes, if external systems need to push occasional ticks directly. For high‑volume systematic feeds, Kafka or another broker is usually the primary channel. The HTTP endpoint can remain for back‑office tools, manual fixes, or integration tests.

### Q: How would you publish anomalies to Kafka?
**A:** The anomaly repository could raise events whenever a new anomaly is added, or the processor could publish directly. These events would be written to an `anomalies` topic, where downstream consumers (alerting services, storage pipelines, dashboards) can subscribe and react independently of the core processor.

### Q: What serialization format would you choose for Kafka messages?
**A:** In a polyglot environment, I would favour something schema‑driven like Avro or Protobuf, with schema evolution handled via a schema registry. JSON is simpler but less compact. For this educational project, JSON would be fine, but in production I would lean towards a binary format for performance and safety.

### Q: How does using Kafka change backpressure handling?
**A:** Kafka naturally buffers messages on disk, so the core processor can consume at its own pace up to a point. Backpressure moves from memory pressure (channels filling) to lag on Kafka partitions. We would monitor consumer lag and scale processor instances horizontally when lag grows beyond acceptable thresholds.

### Q: How would you write tests against Kafka integration without a full cluster?
**A:** Use a lightweight test harness like a Docker‑based Kafka container (e.g., via Testcontainers) in integration tests. For pure unit tests, mock the consumer interface and feed synthetic messages directly into the processor.

### Q: How do you deal with exactly‑once semantics in Kafka?
**A:** Achieving true exactly‑once semantics end‑to‑end is complex. In this project, I would aim for at‑least‑once with idempotent consumers: use message keys + sequence numbers, store processed offsets transactionally alongside state, and design operations to be idempotent. If strict guarantees are required, we can explore Kafka transactions, but that adds complexity.

### Q: Would you expose your own events to external teams?
**A:** Yes, anomalies and perhaps aggregated statistics could be published to well‑defined topics with documented schemas. This turns the service into a platform others can build on, while the HTTP API remains suitable for synchronous queries and dashboards.

### Q: How do you prevent the processing engine from being tightly coupled to Kafka?
**A:** By treating Kafka as just another implementation detail behind a producer interface or a hosted service that calls into `IMarketDataProcessor`. The processor itself knows nothing about Kafka; it simply receives `PriceUpdate` calls. This keeps the core domain and application code independent of the messaging technology.

---

## 19) Scaling & Distributed Systems

### Q: How would you scale this service horizontally?
**A:** Run multiple instances behind a load balancer. Each instance has its own set of partitions and workers. For HTTP‑based ingestion, the load balancer spreads ticks across instances. For Kafka, consumer groups handle assignment of partitions to instances. State is currently in‑memory, so each instance maintains its own view; with a DB, stats can be shared.

### Q: How do you avoid “split brain” between instances if state is in memory?
**A:** Today, each instance keeps its own independent state. Clients must know which instance they are querying, or we put a database in front of the read endpoints to provide a shared view. For the assignment, split‑brain is acceptable because the focus is on the processing engine, not cross‑instance consistency.

### Q: Could you shard by symbol ranges across nodes?
**A:** Yes. For example, we could assign symbols starting with A–F to node 1, G–L to node 2, etc., or use a consistent hashing scheme. A gateway or message router would need to route ticks based on this scheme so each symbol always hits the same node, preserving ordering and single‑writer semantics.

### Q: How would you detect that one node is overloaded compared to others?
**A:** By exposing per‑node metrics (queue sizes, CPU, throughput) and aggregating them in a monitoring system. If one node shows much higher queue backlog or CPU usage, it likely has a skewed symbol distribution or hotspot. This can be mitigated by rebalancing symbols or adding more nodes and adjusting routing.

### Q: How do you handle node failures in a multi‑node deployment?
**A:** If a node crashes, its in‑memory state is lost. With persistence, a replacement node can reconstruct state from the database or replay events from Kafka. Clients may see a brief gap in statistics but the system as a whole can recover. Load balancers or service discovery should stop routing traffic to failed instances.

### Q: What about clock synchronization across nodes?
**A:** Anomaly detection uses timestamps. If producers send timestamps, skew between producer clocks could cause inaccuracies. In a multi‑node setup, I would prefer using server‑side timestamps upon ingestion, and rely on NTP to keep node clocks within a reasonable bound. For extreme precision trading systems, more advanced clocking (e.g., PTP) might be needed.

### Q: How would you support multi‑region deployment?
**A:** Options include active‑active regions with local processing and eventual consistency between them, or a primary region with warm standby. Symbol sharding by region or product makes it easier to keep related traffic local. Cross‑region replication of persistent data must be designed carefully to avoid conflicts and high latency.

### Q: Would you use a distributed cache (like Redis) here?
**A:** Redis could be used as a shared cache for symbol statistics in a multi‑instance deployment, reducing recomputation and providing a consistent view across nodes. However, we must balance cache invalidation complexity and network latency against the benefits. For the assignment, in‑memory caches per node are sufficient.

### Q: How do you think about CAP theorem for this system?
**A:** If we introduce distributed data stores, we must choose trade‑offs. For real‑time stats and anomalies, availability is usually more important than strict global consistency; some dashboards can tolerate slightly stale or approximate data. So we might favour AP or “soft” CP designs depending on business needs.

### Q: How would you explain scaling strategy to a non‑technical product manager?
**A:** I would say: “We can run multiple copies of this service behind a load balancer. When traffic grows, we add more copies. Each copy handles part of the data stream. If one copy fails, the others keep working, and we can rebuild the lost state from persistent storage if needed.”

---

## 20) Testing Deep Dive

### Q: How would you unit test the moving average logic?
**A:** Create a `MovingAverageBuffer` with a known capacity, push a deterministic sequence of prices, and assert the expected average after each push. Include cases where the buffer wraps around and where all values are identical, plus floating‑point edge cases like very large or small numbers.

### Q: How would you test the sliding window min/max behaviour?
**A:** Use a fake clock or controlled timestamps, insert samples at known times and prices, advance the clock, and assert that old entries are evicted. Check that min and max track the correct extremes when new samples exceed or fall below the current values, and that after eviction the window reflects only the recent data.

### Q: How do you test anomaly detection end‑to‑end for a single symbol?
**A:** Spin up a processor with one partition, feed a sequence of prices that intentionally triggers both upward and downward spikes relative to the min/max in the last second, and assert that `IAnomalyRepository` contains anomalies with correct direction, magnitude, and timestamps. Also test sequences that *do not* trigger anomalies to confirm there are no false positives for small changes.

### Q: How would you test that ordering per symbol is preserved?
**A:** In an integration test, send numbered ticks for a symbol in a known order and have a test hook in the worker or repository record the order in which they were processed. Assert that the processed order matches the sent order. This verifies that partitioning and channel usage preserve per‑symbol ordering.

### Q: How do you test error handling in worker loops?
**A:** Inject a faulting implementation (e.g., a repository that throws on a specific condition) and ensure the worker catches and logs the exception instead of crashing the entire host. For more advanced behaviour, we could count retries or verify that a failing partition is restarted.

### Q: What is your approach to testing configuration?
**A:** I would write tests that bind strongly‑typed options from synthetic configuration sources, checking that default values and validation rules behave as expected (e.g., negative `ChannelCapacity` throws). This ensures that production configuration mistakes are caught early and that option classes are aligned with documentation.

### Q: How do you test the API without hitting a real processor implementation?
**A:** Use a fake `IMarketDataProcessor` registered in DI for tests, which records received updates instead of doing real processing. This allows HTTP tests to focus on routing, status codes, and validation while keeping the processing logic out of scope.

### Q: How would you organise tests in the solution?
**A:** I would create separate test projects: `MarketData.Domain.Tests`, `MarketData.Infrastructure.Tests`, and `MarketData.API.Tests`. Unit tests live close to their respective layers; integration tests may live in the Infrastructure or API test projects, depending on scope. Naming tests by behaviour (e.g., `MovingAverageBufferTests`, `AnomalyDetectionTests`) makes navigation easier.

### Q: How do you test under high load without making tests flaky?
**A:** Use dedicated performance/load test suites that are not part of the regular unit test run. They can be triggered manually or on a nightly CI job. Within those tests, prefer deterministic workloads and stable environments (e.g., fixed hardware, controlled GC settings) to reduce noise, and assert on ranges rather than exact timing.

### Q: What is your strategy for regression testing when changing internals of the processor?
**A:** Keep public contracts (DTOs, repository interfaces) stable and rely on their tests. Add regression tests that reproduce specific bugs or edge cases once, and keep them in the suite permanently. Any internal refactoring must pass these tests, ensuring that behaviour stays consistent even when implementation details change.

---

## 21) Observability & Monitoring

### Q: What metrics would you expose from this service?
**A:** Core metrics include: ticks processed per second, anomalies detected per second, queue length per partition, average/99th percentile processing latency per tick, and error counts (e.g., failed enqueues, exceptions in workers). API metrics like request rate, latency, and status code counts are also useful.

### Q: How would you implement metrics in .NET?
**A:** I would use `System.Diagnostics.Metrics` (OpenTelemetry) or a library like Prometheus‑net to create counters and histograms. These metrics would be registered in DI, updated inside the processor and API, and scraped or collected by a monitoring system like Prometheus or Application Insights.

### Q: Why are logs alone not enough?
**A:** Logs are great for debugging individual incidents, but they are unstructured and high‑volume. Metrics give a quantitative, time‑series view of the system’s behaviour and are much better for alerting and capacity planning. Traces add the missing piece by showing end‑to‑end flows for specific requests.

### Q: How would you log anomalies?
**A:** Each anomaly creation could log a structured event with symbol, direction, magnitude, and timestamp. In production, we might log only aggregated or sampled data to avoid log flooding, and rely on the anomaly store or Kafka topic as the primary source of truth for detailed events.

### Q: How do you avoid logging sensitive data?
**A:** In this domain, there is little PII, but as a rule we avoid logging anything that identifies end‑users or credentials. For market data, logging symbol names and numeric values is usually acceptable, but we still avoid logging full request bodies unnecessarily and respect any compliance requirements from the business.

### Q: What would a “high queue length” alert look like?
**A:** For example: “If any partition queue length exceeds 80% of its capacity for longer than 60 seconds, trigger a warning alert.” The alert message should include the partition ID, current queue size, and recent throughput so an operator can quickly identify bottlenecks and decide whether to scale out or tune configuration.

### Q: How would you trace a single anomalous spike through the system?
**A:** If we include correlation IDs, we can tag the original ingestion request (or Kafka message) with an ID, propagate it through the processor, and include it in anomaly logs or events. Then, tracing tools can show the path from HTTP request or message consumption all the way to anomaly emission and storage.

### Q: How do you ensure observability doesn’t hurt performance too much?
**A:** Keep hot‑path metrics lightweight and avoid logging synchronous I/O in workers. Sampling can reduce the cost of detailed traces. For high‑volume events like ticks, metrics should be updated with simple atomic operations, and logs should be reserved for aggregated information or errors, not every successful tick.

---

## 22) Security Deep Dive

### Q: How would you secure this API in production?
**A:** Place it behind an API gateway or reverse proxy that terminates TLS and enforces authentication (e.g., JWT or mTLS). The gateway would handle rate limiting and IP allow‑lists if required. The service would validate claims and authorise access to write and read endpoints separately.

### Q: What is your approach to input validation and avoiding injection?
**A:** Model binding and validation attributes/FluentValidation ensure that only well‑formed requests reach the application layer. Since we do not build SQL strings manually and would use parameterised queries through an ORM or micro‑ORM, injection risk is low. Still, we treat all external input as untrusted and validate length, character set, and ranges.

### Q: How would you protect against denial‑of‑service attacks?
**A:** Layered defences: rate limiting and connection limits at the gateway/load balancer, bounded channels and strict timeouts in the service, and configuration limits on body size and concurrent requests. Alerts on queue length and CPU would help detect ongoing attacks or misbehaving clients early.

### Q: How do you handle secrets (e.g., DB passwords) in configuration?
**A:** In production, secrets should be stored in a secure secret store (Azure Key Vault, AWS Secrets Manager, etc.) and injected into the app at startup. We avoid hard‑coding secrets or putting them into source control. Local development can use user‑secrets or environment variables.

### Q: Would you expose metrics and health endpoints publicly?
**A:** No. Metrics and detailed health endpoints should be restricted to internal networks or protected via authentication, because they can reveal internal structure and load information. For public monitoring, a minimal `GET /health` that returns a simple status is safer.

### Q: How do you think about security testing for this service?
**A:** I would combine automated scanning (e.g., OWASP ZAP) against the HTTP API, manual pen‑testing for business‑logic issues, and code review for security anti‑patterns. Additionally, load testing helps uncover DoS‑style weaknesses.

### Q: What would you log for security‑relevant events?
**A:** Repeated authentication failures, unusually high request rates from single IPs or tokens, malformed requests that hit validation errors, and any unexpected exceptions at the API boundary. These logs should include correlation IDs and minimal necessary context for investigation.

### Q: How do you balance security with developer productivity here?
**A:** Keep local development friction low (e.g., optional auth or local bypass), but ensure production has strict controls. Use configuration flags and environments so security features (auth, TLS, rate limiting) can be toggled appropriately without changing code between dev and prod builds.

---

## 23) API Design & Versioning

### Q: Why return DTOs instead of exposing domain entities directly?
**A:** DTOs decouple the external contract from internal representation. We can change domain properties, add internal fields, or refactor entities without breaking API consumers. DTOs also provide a clear place to adapt naming and representation for external clients.

### Q: How would you add API versioning?
**A:** Use ASP.NET Core API Versioning or simple route‑based versioning (`/api/v1/prices`). Each version gets its own controllers and DTOs. Deprecated versions stay available for a time but are not extended with new features, encouraging clients to migrate at their own pace.

### Q: How do you decide between REST and something like gRPC here?
**A:** REST/JSON is great for browser‑based dashboards and ad‑hoc clients. gRPC is more efficient and strongly typed for service‑to‑service communication. In a real system, we might offer both: REST for UI and gRPC for other backend services needing low‑latency queries or streaming updates.

### Q: What status codes do you use for common scenarios?
**A:** `200 OK` for successful reads, `202 Accepted` or `200 OK` for successful ingestion depending on semantics, `400 Bad Request` for validation failures, `404 Not Found` when a symbol is unknown, and `500 Internal Server Error` for unexpected failures. Optionally, `429 Too Many Requests` could signal rate limiting at the gateway.

### Q: How do you document the API?
**A:** Use Swagger/OpenAPI generated from controllers and DTOs, enriched with XML comments for summary and parameter descriptions. The `docs/API.md` document complements Swagger with higher‑level diagrams and example flows.

### Q: How do you prevent breaking changes when evolving the API?
**A:** Avoid removing fields or changing their meaning; instead, mark them as deprecated and add new ones. Use versioning when bigger changes are unavoidable. Add contract tests to catch accidental changes to JSON shape or status codes.

### Q: Would you ever add WebSocket or server‑sent events?
**A:** Yes, for real‑time dashboards, push‑based models can reduce polling overhead and provide lower latency updates. The processor could push anomalies or stats to a hub, and clients subscribe to relevant symbols. This would be an extension on top of, not a replacement for, the current REST API.

### Q: How do you design APIs so they are easy to consume from multiple languages?
**A:** Keep payloads simple and self‑describing, avoid deeply nested structures, document all fields, and use conventional HTTP semantics. If stronger typing is needed across languages, consider Protobuf/gRPC definitions with generated clients.

### Q: How would you test API contracts between teams?
**A:** Use contract testing (e.g., Pact) or shared OpenAPI/Protobuf definitions that both sides treat as the source of truth. In tests, verify that producers and consumers can communicate using those contracts without relying on shared implementation details.

### Q: How do you handle time zones and timestamps in the API?
**A:** Use ISO 8601 UTC timestamps in responses and requests. Internally, store and operate on UTC. If clients need local times, they can convert on their side with clear documentation about the reference time zone.

---

## 24) Operations & Incident Management

### Q: How would you run this service in Kubernetes?
**A:** Package the API as a container, deploy it as a Deployment with appropriate resource limits and liveness/readiness probes (using `/health`). Expose it via a Service and Ingress. ConfigMaps and Secrets hold configuration and credentials. Horizontal Pod Autoscalers can scale replicas based on CPU or custom metrics like queue length.

### Q: What are typical operational runbooks you would prepare?
**A:** Runbooks for “high queue length”, “anomaly rate drop to zero”, “API latency spike”, and “node out of memory”. Each runbook defines symptoms, diagnostic steps (metrics to check, logs to inspect), and mitigation actions (scaling, config changes, fail‑over). `docs/OPERATIONS_RUNBOOK.md` is the starting point for these recipes.

### Q: How do you roll out new versions safely?
**A:** Use blue‑green or canary deployments. Start with a small percentage of traffic hitting the new version, monitor metrics and logs, then gradually increase. Ensure that schema and API changes are backward‑compatible during the rollout window.

### Q: How do you handle configuration changes without restarts?
**A:** For simple setups, config changes require restarts, but we can minimise disruption with rolling restarts. For advanced setups, .NET options can be bound from reloadable configuration providers (e.g., files with `reloadOnChange`, or dynamic config services), with options monitor patterns to react to changes at runtime. For performance‑critical settings, I prefer restart‑based changes for predictability.

### Q: What signals tell you the service is healthy?
**A:** Low error rate, stable queue sizes, expected throughput, consistent anomaly rates given market conditions, acceptable API latencies, and no unusual CPU/memory spikes. Health checks should reflect both infrastructure (process up) and application state (e.g., worker loops running).

### Q: What is your approach to on‑call for this kind of service?
**A:** Define clear SLIs/SLOs (availability, latency, data freshness), alert only on symptoms that threaten these SLOs, and provide runbooks so on‑call engineers have concrete next steps. Post‑incident reviews help refine alerts to reduce noise and improve coverage.

### Q: How would you simulate a major incident for practice?
**A:** Run game days where we intentionally inject failures: kill a pod, break DB connectivity, overload the service with traffic, or misconfigure simulation. Observe how the team uses dashboards and runbooks to detect and resolve the issue, then refine tooling and documentation afterwards.

### Q: How do you decide which metrics to alert on vs only dashboard?
**A:** Alert on metrics directly related to user‑visible impact (e.g., error rate, high latency, persistent queue growth). Use dashboards for diagnostic metrics (GC pauses, cache hit rates, detailed per‑partition stats) that are useful during investigation but too noisy or low‑level for direct alerts.

---

## 25) Refactoring & Evolution

### Q: How would you introduce a new anomaly rule without breaking existing behaviour?
**A:** Implement the new rule side‑by‑side with the old one behind a feature flag. Initially, log what the new rule *would* have produced without exposing it to users. Compare results offline; once satisfied, flip the flag to enable the new rule, possibly still emitting both anomaly types for a period to validate.

### Q: How do you keep technical debt under control in this project?
**A:** Periodically review the code for shortcuts taken during initial implementation (e.g., missing tests, TODOs) and prioritise them in the backlog. Use code reviews to enforce architectural boundaries and coding standards so new debt doesn’t accumulate unnoticed.

### Q: What part of the code would you expect to change most frequently?
**A:** Business rules like anomaly detection thresholds, moving average window sizes, or configuration for partitioning and simulation. The architecture and infrastructure code should change less often, which is why it’s important to design it carefully up front.

### Q: How do you ensure refactoring doesn’t break existing clients?
**A:** Maintain a strong automated test suite, particularly around public contracts (DTOs, APIs) and core algorithms. Use semantic versioning for API changes, deprecate but do not immediately remove old endpoints, and communicate changes clearly to consumers.

### Q: When would you split a handler into multiple handlers?
**A:** If a handler grows to handle multiple distinct responsibilities or if it starts coordinating too many collaborators, it becomes a candidate for splitting. For example, processing a tick and persisting it might be separated into a `ProcessTickHandler` and a `PersistTickHandler` invoked via an event.

### Q: How do you avoid “architecture astronautics” while still showing senior design?
**A:** By keeping layers and abstractions justified by concrete requirements: clear testability, potential for future persistence, and real‑time processing constraints. We avoid generic abstractions that are not used and keep the code base small and focused.

### Q: How do you know it’s time to modularise or extract a library?
**A:** When multiple projects or teams start depending on the same logic (e.g., common analytics, DTOs, or infrastructure helpers) and changes in one place risk breaking others. At that point, extracting a shared library with clear versioning helps control change.

### Q: How would you explain the evolution path of this project to an interviewer?
**A:** I would describe the current state as “single service, in‑memory analytics, simple anomalies” and outline incremental steps: add persistence, improve observability, integrate Kafka, consider sharding, and eventually introduce more advanced anomaly models. Each step leverages existing abstractions so we don’t need a full rewrite.

---

## 26) Language / Framework Details

### Q: Why .NET and C# for this type of system?
**A:** .NET offers high performance, a mature GC, great async support, and strong tooling. C# has modern language features (async/await, spans, records) that make it convenient to write both high‑level application code and low‑level performance‑sensitive code when needed.

### Q: How does the .NET thread pool affect this design?
**A:** The thread pool manages the actual OS threads that run our tasks. Worker loops and request handlers are tasks scheduled on this pool. We don’t hard‑code thread counts; instead, we rely on the pool to grow and shrink as needed. This makes the design more adaptive across different environments.

### Q: What GC considerations are there for a high‑throughput service?
**A:** Excessive allocations lead to frequent collections and pause times. By using fixed buffers and avoiding per‑tick allocations, we reduce GC pressure. In production, we might tune the GC mode (Server GC, latency settings) and monitor GC metrics to ensure pauses are acceptable.

### Q: Would you consider using `Span<T>` or `Memory<T>` here?
**A:** For the moving average and sliding window, we already use arrays directly, which are efficient. If we later process slices of large data buffers, `Span<T>` could help avoid copying without sacrificing safety, but it’s not strictly necessary in the current design.

### Q: How do you handle exceptions in async code correctly?
**A:** Always `await` tasks or explicitly handle them, avoid `async void` except for event handlers, and use try/catch inside async methods where we can handle or log errors. In hosted services, unobserved exceptions can crash the process, so we wrap worker loops and log failures explicitly.

### Q: How does dependency injection help testability in this project?
**A:** DI lets us inject fake implementations of interfaces like `IMarketDataProcessor`, repositories, and loggers in tests. It also centralises configuration in `Program.cs`/startup, making the runtime wiring explicit and easy to adjust per environment.

### Q: Why use records or immutable types for some DTOs?
**A:** Immutable DTOs help avoid accidental mutation when data flows between layers and threads. They make reasoning about data simpler and reduce bugs where one part of the code changes state that another part depends on.

### Q: How do you structure namespaces in this solution?
**A:** Namespaces roughly mirror folder structure and layer: `MarketData.Domain.Entities`, `MarketData.Application.Commands`, `MarketData.Infrastructure.Processing`, etc. This makes it easy to understand where a type “belongs” and keeps dependencies clear.

### Q: Would you ever use source generators here?
**A:** Potentially, for repetitive mapping code between entities and DTOs, or for strongly‑typed configuration binding. For this assignment, manual mapping is fine, but in a larger code base generators can reduce boilerplate while keeping performance high.

### Q: How do you keep up with changes in .NET that might affect this design?
**A:** By following release notes and performance blogs, experimenting with new features in isolated branches, and refactoring the code opportunistically when a new feature brings clear benefits (e.g., better async primitives, improved collections). The clean layering makes such changes easier to apply gradually.

