# MarketDataSystem – Documentation Index

This folder contains the **full documentation set** for the MarketDataSystem project.

The goal is to make the system **fully understandable** for:
- reviewers,
- interviewers,
- and future contributors,

with **no ambiguity** about design, behavior, or trade-offs.

## Documents Overview

| File                              | Purpose                                                                                     |
|-----------------------------------|---------------------------------------------------------------------------------------------|
| `README_DOCS.md`                  | This index – how the documentation is organized.                                            |
| `ARCHITECTURE-COMBINED.md`        | Full architecture deep-dive with diagrams and timelines.                                    |
| `INTERVIEW_QA.md`                 | Long-form Q&A in interview style (from high-level to low-level details).                    |
| `API.md`                          | HTTP API reference (endpoints, contracts, examples).                                        |
| `DESIGN_DECISIONS.md`             | Key architectural decisions and their rationale (ADR-style).                                |
| `PERFORMANCE.md`                  | Performance characteristics, scaling model, and load-testing approach.                      |
| `TESTING_STRATEGY.md`             | How to test the system at different levels (unit, integration, load).                      |
| `OPERATIONS_RUNBOOK.md`           | Operational guide: how to run, monitor, and troubleshoot the system in practice.           |
| `SIMULATION_GUIDE.md`             | How the simulated market feed works and how to use it.                                      |
| `LIMITATIONS_AND_FUTURE_WORK.md`  | Known limitations today, and concrete ideas for next steps.                                 |
| `GLOSSARY.md`                     | Definitions of important terms used throughout the project and docs.                        |

## Big Picture Diagram

```mermaid
flowchart LR
    subgraph Clients
        C1[Trading App]
        C2[Monitoring Dashboard]
        C3[Load Generator / Simulator]
    end

    subgraph API[MarketData.API]
        CTRL[ASP.NET Core Controllers]
    end

    subgraph APP[Application Layer]
        CMD[Commands]
        QRY[Queries]
        INTF[Interfaces]
    end

    subgraph INF[Infrastructure Layer]
        PROC[HighPerformanceMarketDataProcessorService]
        REPO[InMemoryStatisticsRepository]
        ANO[InMemoryAnomalyRepository]
        SIM[SimulatedMarketDataFeedHostedService]
    end

    subgraph DOM[Domain Layer]
        PU[PriceUpdate]
        SS[SymbolStatistics]
        PA[PriceAnomaly]
    end

    C1 -->|POST /api/prices| CTRL
    C2 -->|GET /api/prices/{symbol}| CTRL
    C2 -->|GET /api/anomalies| CTRL
    C3 -->|Optional direct HTTP load| CTRL

    CTRL --> CMD --> INTF --> PROC
    CTRL --> QRY --> INTF --> REPO
    PROC --> DOM
    REPO --> DOM
    PROC --> ANO
    SIM --> PROC
```

Use this file as your **starting point**: follow the links to dive into specific topics as needed.
