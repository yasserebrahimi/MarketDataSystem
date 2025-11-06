# MarketDataSystem – HTTP API Reference

This document describes the **public HTTP API** exposed by the `MarketData.API` project.

All examples assume a base URL similar to:

- `https://localhost:5001`
- or `http://localhost:5000`

(Your actual port may differ depending on configuration.)

---

## 1. Overview

The API follows a **CQRS-style** approach:

- Write-side:
  - `POST /api/prices` – enqueue new price updates.
- Read-side:
  - `GET /api/prices/{symbol}` – read statistics for one symbol.
  - `GET /api/prices` – read statistics for all symbols.
  - `GET /api/anomalies` – read recent anomalies.
  - `GET /api/metrics` – read processing metrics.
  - `GET /health` – health check for orchestrators/monitoring.

### 1.1 API Shape Diagram

```mermaid
flowchart TB
    subgraph Write[Write-side]
        P[POST /api/prices]
    end

    subgraph Read[Read-side]
        G1[GET /api/prices/{symbol}]
        G2[GET /api/prices]
        G3[GET /api/anomalies]
        G4[GET /api/metrics]
        H[GET /health]
    end
```

---

## 2. POST /api/prices

Enqueues a new **price update** for a symbol into the real-time processing pipeline.

- **Method**: `POST`
- **URL**: `/api/prices`
- **Body (JSON)**:

```json
{
  "symbol": "EURUSD",
  "price": 1.0853,
  "timestamp": "2025-11-06T10:00:00Z"
}
```

- `symbol` (string, required):
  - Non-empty, max length ~32 chars (enforced by validation).
- `price` (number, required):
  - Must be > 0.
- `timestamp` (ISO-8601, optional):
  - If omitted, server may use current UTC time.

### Example Request

```bash
curl -X POST https://localhost:5001/api/prices \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "EURUSD",
    "price": 1.0853,
    "timestamp": "2025-11-06T10:00:00Z"
  }'
```

### Example Response

```json
{
  "success": true,
  "message": "Price update enqueued successfully."
}
```

Typical status codes:

| Code | Meaning                              |
|------|--------------------------------------|
| 200  | Update accepted and enqueued         |
| 400  | Validation failed                    |
| 500  | Internal server error                |

---

## 3. GET /api/prices/{symbol}

Returns **aggregated statistics** for a single symbol.

- **Method**: `GET`
- **URL**: `/api/prices/{symbol}`

Example:

```bash
curl https://localhost:5001/api/prices/EURUSD
```

### Example Response

```json
{
  "symbol": "EURUSD",
  "currentPrice": 1.0853,
  "movingAverage": 1.0849,
  "updateCount": 1203,
  "lastUpdateTime": "2025-11-06T10:00:02.1234567Z",
  "minPrice": 1.0800,
  "maxPrice": 1.0900
}
```

Fields:

| Field          | Type    | Description                                       |
|----------------|---------|---------------------------------------------------|
| `symbol`       | string  | Symbol key (e.g., `EURUSD`, `AAPL`).             |
| `currentPrice` | number  | Latest observed price.                            |
| `movingAverage`| number  | Moving average over last N ticks.                 |
| `updateCount`  | number  | How many ticks have been processed for this symbol. |
| `lastUpdateTime` | string| Timestamp of the last update.                    |
| `minPrice`     | number  | Minimum observed price (across lifetime).         |
| `maxPrice`     | number  | Maximum observed price (across lifetime).         |

Possible responses:

| Code | Meaning                                               |
|------|-------------------------------------------------------|
| 200  | Statistics found and returned                         |
| 404  | Symbol has no data yet                                |
| 500  | Internal server error                                 |

---

## 4. GET /api/prices

Returns **statistics for all symbols** currently tracked.

- **Method**: `GET`
- **URL**: `/api/prices`

Example:

```bash
curl https://localhost:5001/api/prices
```

### Example Response

```json
[
  {
    "symbol": "EURUSD",
    "currentPrice": 1.0853,
    "movingAverage": 1.0849,
    "updateCount": 1203,
    "lastUpdateTime": "2025-11-06T10:00:02.1234567Z",
    "minPrice": 1.0800,
    "maxPrice": 1.0900
  },
  {
    "symbol": "AAPL",
    "currentPrice": 190.12,
    "movingAverage": 189.90,
    "updateCount": 998,
    "lastUpdateTime": "2025-11-06T10:00:02.1000000Z",
    "minPrice": 188.10,
    "maxPrice": 191.50
  }
]
```

This endpoint is useful for dashboards that want to show a **summary table** of all symbols.

---

## 5. GET /api/anomalies

Returns **recent anomalies** (price spikes) detected by the system.

- **Method**: `GET`
- **URL**: `/api/anomalies`
- **Query parameters**:

| Name   | Type   | Required | Default | Description                                             |
|--------|--------|----------|---------|---------------------------------------------------------|
| symbol | string | no       | null    | If provided, filter anomalies for this symbol only.     |
| take   | int    | no       | 100     | Max number of anomalies to return (newest first).       |

Example:

```bash
curl "https://localhost:5001/api/anomalies?symbol=EURUSD&take=20"
```

### Example Response

```json
[
  {
    "symbol": "EURUSD",
    "referencePrice": 1.0800,
    "newPrice": 1.1020,
    "changePercent": 2.0370,
    "timestamp": "2025-11-06T10:00:01.0000000Z",
    "direction": "Up"
  }
]
```

---

## 6. GET /api/metrics

Returns **processing metrics** from the high-performance processor.

- **Method**: `GET`
- **URL**: `/api/metrics`

Example:

```bash
curl https://localhost:5001/api/metrics
```

Example response shape (pseudo):

```json
{
  "totalProcessedTicks": 2000000,
  "anomaliesDetected": 123,
  "activeSymbols": 42,
  "queueSize": 1500,
  "throughputPerSecond": 9850.5
}
```

(Field names may vary slightly depending on the exact DTO in code.)

---

## 7. GET /health

Liveness/health probe.

- **Method**: `GET`
- **URL**: `/health`
- **Response**: HTTP 200 when the service is running.

This endpoint is typically used by:

- Kubernetes liveness/readiness probes,
- Load balancers,
- Monitoring systems.

---

## 8. End-to-End Sequence Diagram

The following Mermaid diagram shows **write + read** flow for one symbol:

```mermaid
sequenceDiagram
    autonumber
    participant Client
    participant API as PricesController
    participant Med as MediatR
    participant CmdHandler as ProcessPriceUpdateHandler
    participant Processor as HighPerformanceMarketDataProcessorService
    participant Worker as PartitionWorker
    participant Repo as InMemoryStatisticsRepository

    Client->>API: POST /api/prices (symbol, price, timestamp)
    API->>Med: Send(ProcessPriceUpdateCommand)
    Med->>CmdHandler: Handle(command)
    CmdHandler->>Processor: EnqueueUpdateAsync(PriceUpdate)
    Processor->>Worker: Write into Channel(partition)
    Worker->>Worker: Update MovingAverage & SlidingWindow
    Worker->>Worker: Detect anomaly (if any)
    Worker->>Worker: Update SymbolStatistics

    Note over Client,API: Later...
    Client->>API: GET /api/prices/{symbol}
    API->>Med: Send(GetSymbolStatisticsQuery)
    Med->>Repo: GetBySymbolAsync(symbol)
    Repo->>Processor: TryGetSymbolStatistics(symbol)
    Processor-->>Repo: SymbolStatistics snapshot
    Repo-->>Med: SymbolStatistics
    Med-->>API: SymbolStatisticsDto
    API-->>Client: 200 OK (JSON)
```


---

## 9. Error Handling Flow (Diagram)

```mermaid
flowchart TD
    Client --> API[ASP.NET Core Controller]
    API --> VAL[FluentValidation / ModelState]

    VAL -->|Invalid| ERR400[400 Bad Request\nValidation errors]
    VAL -->|Valid| MED[MediatR]

    MED --> HND[Command/Query Handler]
    HND --> DEP[IMarketDataProcessor / Repositories]

    DEP -->|Exception| ERR500[500 Internal Server Error]
    DEP -->|Success| OK200[200 OK]

    ERR400 --> Client
    ERR500 --> Client
    OK200 --> Client
```

This diagram shows the **happy path** vs. validation and unexpected failures.
