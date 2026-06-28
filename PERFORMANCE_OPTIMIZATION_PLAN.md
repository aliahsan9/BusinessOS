# BusinessOS — Performance Optimization Plan

**Date:** June 28, 2026  
**Current Performance Score:** 6.0 / 10  
**Target:** 8.5 / 10 for production launch  
**Auditor:** Senior Angular Developer / Senior .NET Developer / DevOps Engineer

---

## Current State Assessment

| Layer | Score | Key Finding |
|-------|:-----:|-------------|
| Frontend Bundle | 5.5 | Full Bootstrap SCSS import; no tree-shaking |
| Frontend Rendering | 7.0 | OnPush on most components; no virtual scroll |
| Frontend Network | 5.0 | Bulk fetches (pageSize: 500); double loading |
| API Response Time | 7.0 | Good indexes; no compression; no response caching |
| Database Queries | 7.5 | Comprehensive indexes; dashboard cache only |
| Caching | 4.0 | In-memory dashboard cache only; no distributed cache |
| Scalability | 4.5 | Single-instance memory cache; no horizontal scaling path |

---

## Frontend Performance

### F1: Bundle Size Optimization

| Issue | Impact | Fix | Effort | Priority |
|-------|--------|-----|:------:|:--------:|
| Full Bootstrap SCSS import | ~200KB CSS | Import only needed Bootstrap modules (grid, forms, utilities, tables) | 4h | P1 |
| Chart.js all registerables | ~50KB JS | Lazy-load Chart.js only on dashboard/reports routes | 2h | P1 |
| No route preloading | Slower nav | Add `PreloadAllModules` or custom preloading for top 3 routes | 2h | P2 |
| No bundle analysis | Unknown size | Add `webpack-bundle-analyzer` or `source-map-explorer` to CI | 2h | P2 |

**Target:** Initial bundle < 500KB gzipped (currently ~1MB warn threshold).

### F2: Rendering Performance

| Issue | Impact | Fix | Effort | Priority |
|-------|--------|-----|:------:|:--------:|
| Large lists without virtual scroll | DOM bloat at 500+ items | Add `@angular/cdk/scrolling` virtual scroll on list pages | 1d | P1 |
| Reports fetch 500 records | Memory + render time | Server-side aggregation; paginate or use dedicated report endpoints | 2d | P0 |
| Sales dashboard fetch 500 orders | Same | Dedicated sales summary API endpoint | 1d | P0 |
| Double loading indicators | Perceived slowness | Send `X-Skip-Loading` header when page shows skeletons | 4h | P1 |
| Dashboard 8 parallel API calls | Network waterfall | Create single `/api/dashboard/summary` aggregated endpoint | 2d | P1 |
| Navbar navItems not reactive | Unnecessary re-renders | Convert to `computed()` signal | 1h | P3 |

### F3: Network Optimization

| Issue | Impact | Fix | Effort | Priority |
|-------|--------|-----|:------:|:--------:|
| Client-side filtering after fetch | Wasted bandwidth | Move all filters to server-side query params | 2d | P0 |
| No HTTP caching headers | Repeated fetches | Add ETag/Cache-Control on GET endpoints for static data (categories, permissions) | 1d | P2 |
| No request deduplication | Duplicate calls on rapid nav | Implement shareReplay on frequently accessed observables | 4h | P2 |
| Retry on all 5xx (max 2) | Delay on persistent failures | Reduce to 1 retry; add circuit breaker pattern | 4h | P3 |

### F4: Memory Management

| Issue | Impact | Fix | Effort | Priority |
|-------|--------|-----|:------:|:--------:|
| State services never cleared | Memory leak on long sessions | Add reset methods; clear on logout | 2h | P2 |
| Chart instances not destroyed | Canvas memory leak | Implement `ngOnDestroy` cleanup in app-chart | 2h | P2 |
| Large client-side datasets | Memory pressure | Paginate all lists; max pageSize: 100 | 1d | P1 |

---

## Backend Performance

### B1: API Response Optimization

| Issue | Impact | Fix | Effort | Priority |
|-------|--------|-----|:------:|:--------:|
| No response compression | Larger payloads | Add `AddResponseCompression()` with Brotli/Gzip | 2h | P1 |
| No response caching headers | Repeated identical requests | Add `[ResponseCache]` on category/permission list endpoints | 4h | P2 |
| Dashboard 8 separate endpoints | 8 DB round trips | Create aggregated dashboard summary endpoint | 2d | P1 |
| Invoice PDF returns HTML string | Large response | Generate actual PDF bytes; stream response | 2d | P2 |
| No pagination max limit | DoS via pageSize=999999 | Enforce max pageSize: 100 in validation | 1h | P0 |

### B2: Database Query Optimization

| Issue | Impact | Fix | Effort | Priority |
|-------|--------|-----|:------:|:--------:|
| Missing Payment/Expense indexes | Slow as data grows | Add composite indexes (see DATABASE_AUDIT_REPORT) | 4h | P1 |
| Potential N+1 in detail queries | Multiple DB round trips | Audit all handlers for `.Include()` chains; prefer projections | 2d | P1 |
| No query timeout | Connection pool exhaustion | Set `CommandTimeout` on DbContext (30s default) | 1h | P2 |
| Dashboard queries uncached beyond 5min | Stale or slow | Evaluate Redis for distributed cache | 3d | P2 |
| No read replica support | Reporting slows OLTP | Add read connection string for dashboard/report queries | 3d | P3 |

### B3: Caching Strategy

| Current | Target |
|---------|--------|
| In-memory dashboard cache (5 min, per-tenant) | Multi-layer caching |

**Recommended caching layers:**

| Layer | Technology | Data | TTL |
|-------|-----------|------|-----|
| L1 — Response cache | ASP.NET Response Caching | Categories, permissions, settings | 15 min |
| L2 — Application cache | IMemoryCache (current) | Dashboard aggregations | 5 min |
| L3 — Distributed cache | Redis | Dashboard, product catalog, session data | 5–15 min |
| L4 — CDN | Azure CDN / CloudFront | Static assets (Angular build) | 1 day |

**Cache invalidation rules:**
- Product/Category mutation → invalidate catalog cache for tenant
- Order/Invoice mutation → invalidate dashboard cache for tenant
- Settings change → invalidate settings cache for tenant

### B4: Scalability Architecture

| Component | Current | Target (10K businesses) |
|-----------|---------|------------------------|
| API instances | Single | 3+ behind load balancer |
| Database | Single SQL Server | Primary + read replica |
| Cache | In-memory (single instance) | Redis cluster |
| File storage | None | Azure Blob / S3 |
| Background jobs | None | Hangfire with Redis storage |
| Message queue | None | Azure Service Bus / RabbitMQ |

---

## Performance Testing Plan

### Load Testing Targets

| Scenario | Target | Tool |
|----------|--------|------|
| Login | 100 req/s, p95 < 200ms | k6 / NBomber |
| Product list (paginated) | 200 req/s, p95 < 150ms | k6 |
| Create order | 50 req/s, p95 < 500ms | k6 |
| Dashboard load | 50 req/s, p95 < 1s | k6 |
| Concurrent tenants | 100 tenants, 10 users each | k6 |

### Frontend Performance Targets

| Metric | Target | Tool |
|--------|--------|------|
| First Contentful Paint | < 1.5s | Lighthouse |
| Largest Contentful Paint | < 2.5s | Lighthouse |
| Time to Interactive | < 3.5s | Lighthouse |
| Cumulative Layout Shift | < 0.1 | Lighthouse |
| Initial bundle size | < 500KB gzipped | webpack-bundle-analyzer |

---

## Implementation Roadmap

### Phase 1 — Quick Wins (Week 1)

| # | Task | Impact | Effort |
|---|------|--------|:------:|
| 1 | Enforce max pageSize: 100 on all list endpoints | High | 1h |
| 2 | Fix client-side filtering → server-side | High | 2d |
| 3 | Replace pageSize: 500 with dedicated summary APIs | High | 2d |
| 4 | Add response compression (Brotli/Gzip) | Medium | 2h |
| 5 | Send X-Skip-Loading when page has skeletons | Medium | 4h |

### Phase 2 — Caching & Optimization (Week 2–3)

| # | Task | Impact | Effort |
|---|------|--------|:------:|
| 6 | Create aggregated dashboard summary endpoint | High | 2d |
| 7 | Add Redis distributed cache | High | 3d |
| 8 | Implement cache invalidation on mutations | High | 2d |
| 9 | Add missing database indexes | Medium | 4h |
| 10 | Import only needed Bootstrap modules | Medium | 4h |
| 11 | Lazy-load Chart.js | Medium | 2h |

### Phase 3 — Scale Preparation (Week 4–6)

| # | Task | Impact | Effort |
|---|------|--------|:------:|
| 12 | Add virtual scroll to list pages | Medium | 1d |
| 13 | Add read replica for reporting queries | Medium | 3d |
| 14 | Set up k6 load testing in CI | Medium | 2d |
| 15 | Add Lighthouse CI for frontend | Medium | 1d |
| 16 | Configure connection pooling for production | Medium | 2h |
| 17 | Add route preloading | Low | 2h |

### Phase 4 — Enterprise Scale (Month 2–3)

| # | Task | Impact | Effort |
|---|------|--------|:------:|
| 18 | Table partitioning for StockTransaction | High | 1w |
| 19 | CDN for static assets | Medium | 2d |
| 20 | Background job processing (Hangfire) | Medium | 1w |
| 21 | Auto-scaling configuration | Medium | 3d |

---

## Monitoring & Alerting

| Metric | Alert Threshold | Tool |
|--------|:--------------:|------|
| API p95 response time | > 500ms | Application Insights |
| Error rate | > 1% | Application Insights |
| Database query time | > 200ms | EF Core logging / SQL Profiler |
| Memory usage | > 80% | Container metrics |
| Cache hit rate | < 70% | Redis metrics |
| Frontend LCP | > 3s | Lighthouse CI |

---

*Performance plan complete. Cross-reference: [BUSINESSOS_AUDIT_REPORT.md](./BUSINESSOS_AUDIT_REPORT.md)*
