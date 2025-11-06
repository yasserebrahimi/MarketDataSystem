# MarketDataSystem – Limitations & Future Work

This document is intentionally explicit about:

- What the system **does not** do yet.
- How it could be extended in a realistic engineering roadmap.

Being honest about limitations is an important part of senior-level design.

---

## 1. Current Limitations

### 1.1 In-Memory State Only

- All statistics (`SymbolStatistics`) and anomalies (`PriceAnomaly`) are kept **in memory only**.
- On process restart:
  - Symbol state and anomaly history are lost.

Implications:

- Not suitable yet for long-term historical analysis.
- Useful primarily for real-time monitoring and demos.

### 1.2 Single-Service Instance (No Sharding Across Nodes)

- The architecture is designed for **horizontal scalability**, but the sample code:
  - Runs in a single process.
  - Uses partitioning *within* that process only.

Implications:

- For extremely large symbol universes or very high tick rates, you would:
  - Need multiple instances and an external routing/sharding layer.

### 1.3 Simple Anomaly Detection

- Anomalies are based on:
  - Min/max within a sliding 1-second window.
  - A simple percentage threshold (e.g., ±2%).

Implications:

- Works well for obvious spikes.
- Does not capture subtle statistical anomalies or context-aware behavior (volatility, time-of-day, etc.).

### 1.4 No Authentication / Authorization

- Endpoints (POST/GET) are assumed to be internal or non-sensitive.
- There is no built-in:
  - Authentication (JWT, API keys).
  - Role-based authorization.

Implications:

- Not ready for public exposure without an API gateway or additional auth layer.

### 1.5 Minimal Persistence & Reporting

- No database or message bus is integrated.
- No long-term reporting or analytics queries.

Implications:

- Limited to real-time state only.
- Any historical analysis must be done via log exports or future integrations.

---

## 2. Future Work – Technical Enhancements

### 2.1 Persistent Read Model

Add a **database-backed** `IStatisticsRepository`:

- Store snapshots for each symbol periodically.
- Or store every tick (if storage allows).
- Options:
  - Relational DB (SQL Server, PostgreSQL).
  - Time-series DB (InfluxDB, TimescaleDB).

Benefits:

- Durable state across restarts.
- Ability to run offline analytics and reporting.

### 2.2 Persistent Anomaly Store

Add persistence for `PriceAnomaly`:

- Write anomalies to:
  - SQL database.
  - Kafka topic.
  - Event Hub / Kinesis stream.

Benefits:

- Long-term anomaly history.
- Downstream consumers (alerting systems, dashboards).

### 2.3 Distributed Processing / Multi-Node Sharding

Introduce a **sharding strategy**:

- Use a consistent hashing router (or message bus partitioning) to:
  - Route symbols across multiple nodes.
  - Each node runs its own partitioned processor.

Benefits:

- Scale horizontally beyond a single machine.
- Increase resilience (node failure only affects a subset of symbols).

### 2.4 More Advanced Anomaly Algorithms

Extend anomaly detection:

- Use additional features:
  - Volatility.
  - Rolling standard deviation.
  - Exponential moving averages (EWMA).
- Possibly integrate ML models for anomaly detection.

Benefits:

- Fewer false positives.
- Detection of subtle patterns.

---

## 3. Future Work – Operational & DevEx Enhancements

### 3.1 Observability with OpenTelemetry

Integrate:

- Distributed tracing (e.g., per-request and per-tick).
- Metrics:
  - Exposed to Prometheus/Grafana.
- Structured logs.

Benefits:

- Better insight into bottlenecks and failures.
- Cleaner metrics bridge for SRE/DevOps.

### 3.2 Full CI/CD Pipeline

Add:

- Automated build & test on pull requests.
- Docker image build and push.
- Environment-specific config for staging/production.

Benefits:

- Faster iteration.
- Reduced deployment risk.

### 3.3 Administrative API Endpoints

Examples:

- `GET /api/admin/partitions` – show partition configuration.
- `POST /api/admin/reload-config` – reload options at runtime.
- `GET /api/admin/anomalies/count` – aggregated anomaly statistics.

Benefits:

- Operational transparency.
- Easier to manage in production.

---

## 4. Future Work – Product Features

### 4.1 Multi-Tenancy

Add a `TenantId` property and adjust:

- `PriceUpdate`
- `SymbolStatistics`
- Repositories and API endpoints

Benefits:

- Support multiple customers/islands of data in one deployment.

### 4.2 Alerting

Integrate anomalies with:

- Email/SMS/Slack notifications.
- Incident management tools.

Benefits:

- Turn raw anomalies into actionable alerts.

---

## 5. Summary

The current implementation focuses on:

- **Real-time processing**
- **Clean architecture**
- **Demonstrating high-level design skills**

The roadmap above outlines how to evolve this into a full production system over time.


---

## 6. High-Level Roadmap (Illustrative)

```mermaid
gantt
    dateFormat  YYYY-MM-DD
    title MarketDataSystem – Possible Roadmap

    section Core Stability
    Add Test Suite             :done,    des1, 2025-11-01, 2025-11-10
    Improve Observability      :active,  des2, 2025-11-10, 2025-11-20

    section Persistence
    DB-backed Stats Repository :planned, des3, 2025-11-21, 2025-12-05
    Persistent Anomaly Store   :planned, des4, 2025-12-06, 2025-12-20

    section Advanced Features
    ML-based Anomalies         :planned, des5, 2026-01-05, 2026-01-31
    Multi-Node Sharding        :planned, des6, 2026-02-01, 2026-02-28
```

These dates are illustrative; the main purpose is to communicate a **clear sequence** of future improvements.
