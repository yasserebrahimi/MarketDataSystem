# MarketDataSystem – Combined Architecture Overview

This document gives a **deep, combined view** of the architecture, including:

- High-level data flow
- Layered architecture
- Component interactions
- Partitioning model
- Symbol state internals

---

## 1. High-Level Data Flow

```mermaid
flowchart LR
    subgraph ClientLayer
        C1["Trading App"]
        C2["Monitoring Tool"]
    end

    subgraph ApiLayer
        API["MarketData.API
(Controllers)"]
    end

    subgraph AppLayer
        CMD["Commands"]
        QRY["Queries"]
        INTF["Interfaces
IMarketDataProcessor,
IStatisticsRepository"]
    end

    subgraph InfraLayer
        PROC["Processor Service"]
        REPO["Statistics Repository"]
        ANO["Anomaly Repository"]
        SIM["Simulation Service"]
        ANA["Analytics
(MA + SlidingWindow)"]
    end

    subgraph DomainLayer
        PU["PriceUpdate"]
        SS["SymbolStatistics"]
        PA["PriceAnomaly"]
    end

    C1 --> API
    C2 --> API

    API --> CMD
    API --> QRY
    CMD --> INTF --> PROC
    QRY --> INTF --> REPO

    PROC --> ANA
    PROC --> SS
    PROC --> ANO --> PA

    REPO --> SS
    SIM --> PROC
```

This diagram shows how read and write paths share a common core (`Processor`) but remain decoupled at the API/Application level.

---

## 2. Layered Architecture

We follow a simplified Clean Architecture style. Each project represents a layer.

```mermaid
graph TD
    APIProj["MarketData.API
(Presentation)"]
    APPProj["MarketData.Application
(Application)"]
    DOMProj["MarketData.Domain
(Domain)"]
    INFProj["MarketData.Infrastructure
(Infrastructure)"]

    APIProj --> APPProj
    APPProj --> DOMProj
    APPProj --> INFProj
    INFProj --> DOMProj
```

### 2.1 Project Responsibilities

| Project                  | Responsibility                                              |
|--------------------------|------------------------------------------------------------|
| `MarketData.API`         | HTTP interface, controllers, Swagger / DI wiring.          |
| `MarketData.Application` | Commands, queries, validation, DTOs, interfaces.          |
| `MarketData.Domain`      | Core domain entities and value objects.                   |
| `MarketData.Infrastructure` | Processor, repositories, analytics, simulation.        |

---

## 3. Processor Partitioning

```mermaid
flowchart TB
    subgraph ProcessorCore
        RT["Router (hash(symbol) mod N)"]
        subgraph Partitions
            direction LR
            P0["Partition 0
Channel + Worker"]
            P1["Partition 1
Channel + Worker"]
            P2["Partition 2
Channel + Worker"]
            P3["Partition 3
Channel + Worker"]
        end
    end

    IN["Incoming Price Updates"] --> RT
    RT --> P0
    RT --> P1
    RT --> P2
    RT --> P3
```

- All updates for a given symbol always go to the **same partition**.
- Each partition has a **single worker** that owns all state updates for its symbols.

---

## 4. SymbolState Internals

```mermaid
classDiagram
    class SymbolState {
        +string Symbol
        +MovingAverageBuffer MovingAverage
        +SlidingWindow Window
        +SymbolStatistics Statistics
    }

    class MovingAverageBuffer {
        -double[] buffer
        -int index
        -int count
        -double sum
        +MovingAverageBuffer(int capacity)
        +double Add(double value)
    }

    class SlidingWindow {
        -int windowMs
        +SlidingWindow(int windowMs)
        +void AddSample(long timestampMs, double price)
        +bool TryGetMinMax(long nowMs, out double min, out double max)
    }

    class SymbolStatistics {
        +decimal CurrentPrice
        +decimal MovingAverage
        +long UpdateCount
        +decimal MinPrice
        +decimal MaxPrice
        +DateTime LastUpdateTime
        +void Update(decimal price, decimal movingAverage)
        +SymbolStatistics Clone()
    }

    SymbolState --> MovingAverageBuffer
    SymbolState --> SlidingWindow
    SymbolState --> SymbolStatistics
```

---

## 5. Startup Timeline

```mermaid
gantt
    dateFormat  X
    title "Startup & First Tick (Conceptual Timeline)"

    section Host
    "Build Host & DI"           :done, 0, 10
    "Configure Options"         :done, 10, 20
    "Register Hosted Services"  :done, 20, 30

    section Processor
    "Start Processor Workers"   :done, 30, 45

    section Simulation
    "Start Simulation Service"  :done, 45, 60
    "Generate First Ticks"      :done, 60, 80
```

(Values are illustrative – they show the order, not real milliseconds.)

---

## 6. Summary

- Clear separation of concerns (API, Application, Domain, Infrastructure).
- High-throughput pipeline using channels and partitioned workers.
- Efficient data structures for moving averages and sliding windows.
- Dedicated documents in `docs/` dive deeper into performance and testing.
