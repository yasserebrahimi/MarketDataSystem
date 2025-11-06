# MarketDataSystem – Security Testing & Considerations

This document summarizes **security aspects** of the MarketDataSystem and outlines a plan
for **security testing**. It is written at a level appropriate for security reviews and interviews.

---

## 1. Security Scope

The service is primarily:

- An internal backend API for ingesting and exposing market data.
- Assumed to be deployed inside a trusted network (but this can change).

Key aspects:

- HTTP API handling.
- In-memory processing of data.
- Optional exposure to the public internet (behind gateway).

---

## 2. Threat Model (High-Level)

### 2.1 Assets

- Integrity of market data (prices, statistics, anomalies).
- Availability of the service (no easy DoS).
- Confidentiality of any potentially sensitive metadata (e.g., client IDs, if added later).

### 2.2 Potential Attack Vectors

- Unauthenticated public access to endpoints (future extension).
- Malicious or malformed HTTP requests.
- High-volume request floods (DoS).
- Exploits via misconfiguration of hosting environment.

---

## 3. Current Security Posture

### 3.1 Authentication & Authorization

- At present, **no auth** is built into the service.
- Intended usage: internal or behind an API gateway that handles authentication/authorization.

### 3.2 Input Validation

- Request models use **FluentValidation** (or equivalent ASP.NET Core validation).
- Symbol and price are validated for basic constraints.

### 3.3 Transport Security

- Uses default ASP.NET Core hosting; when deployed correctly:
  - HTTPS/TLS termination at reverse proxy or directly in Kestrel.

### 3.4 Data at Rest

- No persistent storage in this sample (in-memory only).
- Persistence would require additional security for the underlying database.

---

## 4. Security Testing Plan

### 4.1 Static Analysis

Use tools like:

- `dotnet format` + analyzers (Roslyn) for code quality.
- Optional: commercial SAST tools (SonarQube, GitHub CodeQL).

Goals:

- Detect unsafe patterns (e.g., improper exception handling).
- Enforce secure coding guidelines.

### 4.2 API Security Testing

Use manual and automated tools (e.g., **OWASP ZAP**, **Burp Suite**) to test:

- Injection (where applicable).
- Insecure direct object references (IDOR) – e.g., symbol names.
- HTTP header configuration (CORS, security headers).

Even though we are not dealing with authentication secrets here, validating the HTTP surface is valuable.

### 4.3 Denial-of-Service (DoS) Testing

Scenarios:

1. **High-rate POST /api/prices**:
   - Ensure bounded channels and backpressure behave as expected.
   - Confirm service does not crash or exhaust memory.

2. **High-rate GET /api/prices** / `/api/anomalies`:
   - Use a load-testing tool to generate read load.
   - Check CPU usage and latency.

Mitigations:

- Bounded channels.
- Potential rate limiting at gateway level (future).

### 4.4 Configuration Hardening

Checklist:

- Enforce HTTPS (no HTTP-only in production).
- Configure Kestrel behind a reverse proxy with TLS.
- Disable detailed error pages in production.
- Validate `MarketDataProcessing` configuration on startup to avoid insecure or broken settings.

---

## 5. Example Security Test Cases

| ID  | Test Case                                         | Expected Result                                      |
|-----|---------------------------------------------------|------------------------------------------------------|
| ST1 | Send malformed JSON to `POST /api/prices`         | 400 Bad Request; no crash.                           |
| ST2 | Send negative/zero price values                   | 400 Bad Request due to validation.                   |
| ST3 | Flood `POST /api/prices` at high rate             | Service remains responsive; no unbounded memory use. |
| ST4 | Flood `GET /api/prices` and `GET /api/anomalies`  | Latency increases gracefully, but service stays up.  |
| ST5 | Try access using HTTP (no TLS, in prod)           | Block/redirect at gateway (depending on setup).      |

---

## 6. Future Security Enhancements

### 6.1 Authentication & Authorization

- Integrate JWT or OAuth2-based bearer tokens.
- Limit read/write endpoints per role:
  - Ingestion services only for POST.
  - Read-only dashboards for GET.

### 6.2 Rate Limiting / Throttling

- Implement rate limiting middleware.
- Or configure limits at API gateway:
  - Requests per second per IP/client.
  - Burst vs sustained limits.

### 6.3 Security Logging & Alerting

- Log security-relevant events:
  - Repeated invalid requests.
  - Unusual spikes from single IP.
- Integrate with SIEM (Security Information and Event Management) tools.

---

## 7. Summary

Even though this project is primarily an architectural and performance exercise:

- We have a clear view of potential security concerns.
- The design (options, repositories, MediatR-based API) is compatible with typical enterprise security standards.
- A concrete testing plan helps to progressively harden the system if it moves towards production.

