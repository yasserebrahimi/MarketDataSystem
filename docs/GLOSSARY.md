# MarketDataSystem – Glossary

This glossary defines key terms used throughout the codebase and documentation.

---

## A

### Anomaly

A detected **price spike** that exceeds a configured threshold (e.g., ±2%) within a given time window (e.g., 1 second).
Represented by the `PriceAnomaly` domain entity.

### Anomaly Repository

An abstraction (`IAnomalyRepository`) and implementation (`InMemoryAnomalyRepository`) that stores recent anomalies
for retrieval via the API (`/api/anomalies`).

---

## B

### Backpressure

A mechanism to **protect the system** from overload by slowing down or dropping incoming work.  
In this system, backpressure is implemented via **bounded channels** with `DropOldest` behavior.

---

## C

### Channel

`System.Threading.Channels.Channel<T>` – an asynchronous producer/consumer queue used to:

- Buffer incoming `PriceUpdate` events.
- Connect producers (commands, simulation) with consumers (worker tasks).

### Clean Architecture

An architectural style that separates:

- Domain logic,
- Application use-cases,
- Infrastructure concerns,
- Presentation layers,

and enforces dependencies pointing **inwards**.

---

## D

### Domain

Core business objects and rules. In this system:

- `PriceUpdate`
- `SymbolStatistics`
- `PriceAnomaly`

are part of the Domain layer (`MarketData.Domain`).

---

## H

### HighPerformanceMarketDataProcessorService

The core processing component that:

- Ingests price updates via channels.
- Maintains per-symbol state (moving average, sliding window, statistics).
- Detects anomalies.

Runs as an `IHostedService` inside the ASP.NET Core host.

---

## M

### Moving Average

A numeric average computed over the **last N price updates** for a symbol. Implemented with:

- `MovingAverageBuffer` – a ring buffer that tracks the rolling sum and count.

### Monotonic Deque

A double-ended queue whose elements are kept in **monotonic order** (non-decreasing or non-increasing).  
Used in `SlidingWindow` to compute min/max in O(1) time.

---

## P

### Partition

A **logical shard** of work inside the processor:

- Each partition has its own `Channel<PriceUpdate>` and worker task.
- Symbols are assigned to partitions via `hash(symbol) % partitionCount`.

### PriceUpdate

A single tick of market data:

- Symbol,
- Price,
- Timestamp.

Source of truth for calculations.

---

## S

### Sliding Window

A time-based window (e.g., 1 second) over recent prices:

- Used to maintain min/max within that window.
- Supports strict “within 1 second” semantics for anomaly detection.

Implemented with `SlidingWindow` + monotonic deques.

### Symbol

A string identifier for a traded instrument or asset:

- e.g., `"EURUSD"`, `"AAPL"`, `"BTCUSD"`.

### SymbolStatistics

Aggregated state for a given symbol:

- Latest price,
- Moving average,
- Update count,
- Last update time,
- Min/max price observed.

---

## T

### Ticks Per Second (TPS)

Number of price updates processed per second. Used both as a performance metric and in simulation configuration
(`Simulation.TicksPerSecond`).

---

This glossary is meant to make reading the rest of the documentation and code **unambiguous**.
