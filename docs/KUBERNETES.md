# Kubernetes Deployment Guide

Complete guide for deploying MarketData to Kubernetes.

---

## Quick Start

### Using Helm (Recommended)

```bash
# Add Bitnami repository (for dependencies)
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update

# Install with Helm
helm install marketdata charts/marketdata \
  --namespace marketdata-production \
  --create-namespace \
  --set secrets.jwtSecretKey="YOUR_SECRET_KEY" \
  --set postgresql.auth.password="DB_PASSWORD"

# Check status
kubectl get all -n marketdata-production
```

### Using Raw Manifests

```bash
# Create namespace
kubectl create namespace marketdata-production

# Create secrets
kubectl create secret generic marketdata-secrets \
  --from-literal=jwt-secret-key="YOUR_SECRET_KEY" \
  --from-literal=db-connection-string="Host=postgres;..." \
  -n marketdata-production

# Apply manifests
kubectl apply -f k8s/base/ -n marketdata-production
```

---

## Architecture

```
┌─────────────────┐
│   Ingress       │  (HTTPS, TLS)
└────────┬────────┘
         │
┌────────▼────────┐
│   Service       │  (ClusterIP:80)
└────────┬────────┘
         │
┌────────▼────────┐
│  Deployment     │  (2-10 replicas with HPA)
│  ┌───────────┐  │
│  │ API Pod   │  │
│  │ - Liveness│  │
│  │ - Readiness│ │
│  └───────────┘  │
└─────────────────┘
```

---

## Helm Chart

### Values Configuration

```yaml
# values.yaml
replicaCount: 3

image:
  repository: yasserebrahimi/marketdata
  tag: "1.0.0"

resources:
  requests:
    cpu: 250m
    memory: 512Mi
  limits:
    cpu: 1000m
    memory: 2Gi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70

postgresql:
  enabled: true
  auth:
    password: "secure_password"

redis:
  enabled: true
```

### Install/Upgrade

```bash
# Install
helm install marketdata charts/marketdata -f values-prod.yaml

# Upgrade
helm upgrade marketdata charts/marketdata -f values-prod.yaml

# Rollback
helm rollback marketdata

# Uninstall
helm uninstall marketdata
```

---

## Resource Definitions

### Deployment

Key features:
- Rolling update strategy (maxSurge: 1, maxUnavailable: 0)
- Health checks (liveness, readiness)
- Resource limits
- Security context (non-root, read-only filesystem)
- Anti-affinity for HA

### Service

```yaml
apiVersion: v1
kind: Service
metadata:
  name: marketdata-api
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: http
  selector:
    app: marketdata
```

### Ingress

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: marketdata-ingress
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - marketdata.example.com
    secretName: marketdata-tls
  rules:
  - host: marketdata.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: marketdata-api
            port:
              number: 80
```

### HPA (Horizontal Pod Autoscaler)

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: marketdata-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: marketdata-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

---

## Monitoring

### Health Checks

```bash
# Check liveness
kubectl exec -it pod/marketdata-api-xxx -- curl http://localhost/health

# Check readiness
kubectl exec -it pod/marketdata-api-xxx -- curl http://localhost/health/ready
```

### Logs

```bash
# View logs
kubectl logs -f deployment/marketdata-api -n marketdata-production

# Tail logs
kubectl logs -f --tail=100 deployment/marketdata-api

# Previous container logs
kubectl logs -f deployment/marketdata-api --previous
```

### Metrics

```bash
# Pod metrics
kubectl top pods -n marketdata-production

# Node metrics
kubectl top nodes
```

---

## Troubleshooting

### Pod Not Starting

```bash
# Describe pod
kubectl describe pod marketdata-api-xxx

# Check events
kubectl get events --sort-by='.lastTimestamp'

# Check logs
kubectl logs marketdata-api-xxx
```

### Common Issues

1. **ImagePullBackOff**: Check image name and registry credentials
2. **CrashLoopBackOff**: Check logs for application errors
3. **Pending**: Check resource availability and node capacity

---

## Scaling

### Manual Scaling

```bash
# Scale replicas
kubectl scale deployment/marketdata-api --replicas=5

# Check status
kubectl get deployment marketdata-api
```

### Auto-scaling

HPA automatically scales based on CPU/memory usage:
- Min: 2 replicas
- Max: 10 replicas
- Target: 70% CPU

---

## Security

### Pod Security

- Non-root user (UID 1000)
- Read-only root filesystem
- Drop all capabilities
- No privilege escalation

### Network Policies

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: marketdata-netpol
spec:
  podSelector:
    matchLabels:
      app: marketdata
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
  egress:
  - to:
    - namespaceSelector: {}
      podSelector:
        matchLabels:
          app: postgres
  - to:
    - namespaceSelector: {}
      podSelector:
        matchLabels:
          app: redis
```

---

## Backup & Restore

### Database Backup

```bash
# Backup PostgreSQL
kubectl exec -it postgres-pod -- pg_dump -U postgres marketdata > backup.sql

# Restore
kubectl exec -i postgres-pod -- psql -U postgres marketdata < backup.sql
```

---

**Last Updated:** 2025-11-06
