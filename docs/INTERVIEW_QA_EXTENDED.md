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
