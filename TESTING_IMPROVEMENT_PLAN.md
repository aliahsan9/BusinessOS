# BusinessOS — Testing Improvement Plan

**Date:** June 28, 2026  
**Current Coverage:** ~28% line / ~18% branch  
**Target:** 90%+ line coverage  
**Auditor:** QA Lead / Senior .NET Developer / Senior Angular Developer

---

## Current State

### Test Inventory

| Project | Tests | Framework | Quality |
|---------|:-----:|-----------|---------|
| BusinessOS.UnitTests | 134 | xUnit + Moq + FluentAssertions | Good for covered domains |
| BusinessOS.IntegrationTests | 77 | xUnit + WebApplicationFactory | Good for covered domains |
| BusinessOS.Web (*.spec.ts) | ~79 | Jasmine/Karma | Mostly scaffold (`should create`) |
| BusinessOS.Web (e2e) | 4 | Playwright | Auth smoke tests only |
| **Total** | **294** | | |

### Coverage by Package (Measured June 28, 2026)

| Package | Line Coverage | Branch Coverage |
|---------|:------------:|:--------------:|
| BusinessOS.Application | 35.6% | 23.8% |
| BusinessOS.Infrastructure | 37.7% | 20.2% |
| BusinessOS.Domain | 36.3% | 14.3% |
| BusinessOS.API | 0.6% | 1.4% |
| **Overall** | **~28.1%** | **~18.1%** |

### Coverage Configuration

- **Tool:** Coverlet with Cobertura output
- **Threshold:** 80% configured in test `.csproj` files
- **Exclusions:** Migrations, DbInitializer, Program.cs, several entity types
- **CI enforcement:** ❌ No CI pipeline to enforce threshold

### What's Tested ✅

| Domain | Unit | Integration | Notes |
|--------|:----:|:-----------:|-------|
| Auth (register/login) | ✅ | ✅ | |
| Categories CRUD | ✅ | ✅ | |
| Products CRUD | ✅ | ✅ | |
| Customers CRUD | ✅ | ✅ | Analytics tested |
| Orders (full lifecycle) | ✅ | ✅ | 13 unit + integration |
| Inventory (stock ops) | ✅ | ✅ | |
| Dashboard/Analytics | ✅ | ✅ | Cache tested |
| RBAC/Permissions | ✅ | ✅ | Authorization enforcement |
| Validators | ✅ | — | 26 validator tests |
| MediatR behaviors | ✅ | — | Validation + logging |
| Pagination | ✅ | — | |
| Database constraints | — | ✅ | FK/unique enforcement |

### What's NOT Tested ❌

| Domain | Unit | Integration | Frontend | E2E |
|--------|:----:|:-----------:|:--------:|:---:|
| Suppliers | ❌ | ❌ | Scaffold | ❌ |
| Purchase Orders | ❌ | ❌ | Scaffold | ❌ |
| Payments | ❌ | ❌ | Scaffold | ❌ |
| Invoices | ❌ | ❌ | Scaffold | ❌ |
| Quotations | ❌ | ❌ | Scaffold | ❌ |
| Expenses | ❌ | ❌ | Scaffold | ❌ |
| Finance/P&L | ❌ | ❌ | ❌ | ❌ |
| Users/Roles admin | ❌ | ❌ | ❌ | ❌ |
| Settings | ❌ | ❌ | ❌ | ❌ |
| Notifications | ❌ | ❌ | ❌ | ❌ |
| Audit | ❌ | ❌ | ❌ | ❌ |
| System Admin | ❌ | ❌ | ❌ | ❌ |
| Security (IDOR) | ❌ | ❌ | — | ❌ |
| Multi-tenant isolation | Partial | Partial | — | ❌ |

---

## Target State (90%+ Coverage)

### Test Count Targets

| Layer | Current | Target | New Tests Needed |
|-------|:-------:|:------:|:----------------:|
| Backend unit | 134 | 500+ | ~370 |
| Backend integration | 77 | 200+ | ~125 |
| Frontend unit | ~79 (scaffold) | 300+ meaningful | ~220 |
| E2E (Playwright) | 4 | 50+ | ~46 |
| **Total** | **294** | **1,050+** | **~760** |

---

## Phase 1: Critical Security & Infrastructure Tests (Week 1–2)

### 1.1 Security Tests (Priority: P0)

| Test | Type | File |
|------|------|------|
| Tenant A cannot access Tenant B users (IDOR) | Integration | `SecurityIntegrationTests.cs` |
| Tenant A cannot create users in Tenant B | Integration | `SecurityIntegrationTests.cs` |
| Unauthenticated requests rejected on all endpoints | Integration | `SecurityIntegrationTests.cs` |
| Permission enforcement on every endpoint group | Integration | Extend `AuthorizationIntegrationTests.cs` |
| JWT with invalid signature rejected | Unit | `JwtTokenGeneratorTests.cs` |
| JWT with expired token rejected | Integration | `AuthIntegrationTests.cs` |
| Tenant middleware rejects missing tenant context | Integration | `TenantMiddlewareTests.cs` |

**Estimated:** 25 integration tests, 10 unit tests

### 1.2 CI Pipeline with Coverage Gate (Priority: P0)

```yaml
# .github/workflows/ci.yml
- dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
- ReportGenerator → coverage report
- Fail if line coverage < 80% (increase to 90% over time)
- ng test --code-coverage (frontend)
```

---

## Phase 2: Backend Domain Coverage (Week 3–6)

### 2.1 Unit Tests — Handlers & Validators

| Domain | Handler Tests | Validator Tests | Est. Count |
|--------|:------------:|:---------------:|:----------:|
| Suppliers | 8 | 4 | 12 |
| Purchase Orders | 10 | 5 | 15 |
| Payments | 8 | 4 | 12 |
| Invoices | 10 | 5 | 15 |
| Quotations | 10 | 5 | 15 |
| Expenses | 8 | 4 | 12 |
| Expense Categories | 6 | 3 | 9 |
| Finance Service | 8 | — | 8 |
| Identity Service | 10 | — | 10 |
| Role Service | 8 | — | 8 |
| Settings Service | 6 | — | 6 |
| Notification Service | 4 | — | 4 |
| Audit Service | 4 | — | 4 |
| System Admin Service | 4 | — | 4 |
| **Subtotal** | | | **~134** |

### 2.2 Integration Tests — Full API Flows

| Flow | Tests | Est. Count |
|------|-------|:----------:|
| Supplier CRUD + link to PO | 5 | 5 |
| Purchase Order create → receive → stock update | 5 | 5 |
| Quotation → convert to order | 4 | 4 |
| Order → create invoice → record payment | 5 | 5 |
| Invoice PDF generation | 2 | 2 |
| Expense CRUD + category management | 5 | 5 |
| Finance P&L calculation | 3 | 3 |
| User CRUD + role assignment | 5 | 5 |
| Settings update + tenant branding | 3 | 3 |
| Notification create + mark read | 3 | 3 |
| Multi-tenant isolation (cross-tenant access denied) | 10 | 10 |
| Concurrent order creation (race condition) | 2 | 2 |
| **Subtotal** | | **~52** |

### 2.3 Infrastructure Tests

| Test | Est. Count |
|------|:----------:|
| DbContext query filter enforcement | 5 |
| Soft delete behavior | 3 |
| TenantId auto-assignment on insert | 3 |
| Cache invalidation on mutation | 3 |
| RBAC seeder correctness | 3 |
| **Subtotal** | **~17** |

---

## Phase 3: Frontend Unit Tests (Week 7–10)

### 3.1 Service Tests (with HttpClientTestingModule)

| Service | Tests | Focus |
|---------|:-----:|-------|
| auth.service | 8 | Login, register, logout, token handling |
| token.service | 10 | Session management, permissions, expiry |
| product.service | 6 | CRUD operations |
| order.service | 6 | CRUD + status update |
| customer.service | 6 | CRUD + analytics |
| inventory.service | 6 | Stock operations |
| All other services (22) | 4 each | Basic CRUD + error handling |
| **Subtotal** | **~120** | |

### 3.2 Component Tests

| Component Type | Tests Each | Count | Focus |
|---------------|:----------:|:-----:|-------|
| List pages (15) | 8 | 120 | Loading, error, empty, data states; pagination; search |
| Form pages (12) | 6 | 72 | Validation, submit, error handling |
| Detail pages (10) | 4 | 40 | Loading, error, data display |
| Shared components (16) | 3 | 48 | Rendering, inputs/outputs, a11y |
| Guards (3) | 4 | 12 | Auth, guest, permission scenarios |
| Interceptors (3) | 4 | 12 | Token injection, loading, error handling |
| **Subtotal** | | **~304** | |

### 3.3 State Service Tests

| State Service | Tests |
|---------------|:-----:|
| dashboard.state | 6 |
| product.state | 6 |
| category.state | 6 |
| inventory.state | 8 |
| **Subtotal** | **~26** |

---

## Phase 4: E2E Tests (Week 11–14)

### 4.1 Critical Business Flows (Playwright)

| Flow | Tests | Priority |
|------|:-----:|:--------:|
| Register → Login → Dashboard | 2 | P0 |
| Create category → product → verify in list | 3 | P0 |
| Create customer → create order → verify | 4 | P0 |
| Create quotation → convert to order | 3 | P0 |
| Order → invoice → payment | 4 | P0 |
| Create supplier → PO → receive goods → stock update | 4 | P1 |
| Inventory stock adjustment | 2 | P1 |
| User login with different roles → verify permissions | 5 | P0 |
| Expense create → view in list | 2 | P1 |
| Settings update → verify persistence | 2 | P1 |
| Search and filter on list pages | 3 | P2 |
| Pagination navigation | 2 | P2 |
| Mobile responsive navigation | 2 | P2 |
| Session expiry → redirect to login | 1 | P1 |
| 403 forbidden page | 1 | P1 |
| **Subtotal** | **~40** | |

### 4.2 E2E Infrastructure

```typescript
// e2e/fixtures/auth.fixture.ts
// Reusable authenticated session for all E2E tests
// Seed test tenant with known data before each suite
```

- Add `globalSetup` to seed test database
- Add authenticated fixture for reuse across tests
- Run against Docker-compose test environment
- Add visual regression testing (optional, Phase 5)

---

## Phase 5: Advanced Testing (Month 4+)

| Type | Tool | Purpose |
|------|------|---------|
| Load testing | k6 / NBomber | API performance under load |
| Contract testing | Pact | Frontend-backend API contract |
| Mutation testing | Stryker.NET | Test quality verification |
| Security scanning | OWASP ZAP | Automated vulnerability scan |
| Accessibility testing | axe-core + Playwright | WCAG compliance |
| Visual regression | Percy / Chromatic | UI consistency |

---

## Coverage Milestones

| Milestone | Timeline | Line Coverage Target | Gate |
|-----------|----------|:--------------------:|:----:|
| M1: Security tests | Week 2 | 35% | CI pipeline live |
| M2: Phase 3/4 backend | Week 6 | 55% | Fail < 50% |
| M3: Frontend services | Week 10 | 70% | Fail < 65% |
| M4: E2E critical paths | Week 14 | 80% | Fail < 75% |
| M5: Full coverage | Week 18 | 90%+ | Fail < 85% |

---

## Test Infrastructure Improvements

| Item | Current | Target |
|------|---------|--------|
| Test database | In-memory (integration) | Docker SQL Server for integration |
| Test data seeding | RBAC seeder only | Full test fixture factory |
| Frontend test DB | None | Mock HTTP with consistent fixtures |
| E2E environment | Local dev server | Docker-compose with API + DB |
| Coverage reporting | Local only | Codecov / Coveralls in CI |
| Test parallelization | Default | Enable xUnit parallel + Playwright sharding |
| Flaky test detection | None | Retry + quarantine in CI |

---

## Test Reliability Guidelines

1. **No test depends on another test's state** — each test seeds its own data
2. **Use test containers** for integration tests requiring real SQL Server
3. **Mock external services** (email, PDF, payment) — never call real APIs in tests
4. **Deterministic test data** — fixed GUIDs, known dates, predictable names
5. **Clean up after tests** — truncate tables or use transaction rollback
6. **Name tests descriptively:** `MethodName_Scenario_ExpectedResult`
7. **One assertion concept per test** — multiple asserts OK if same concept
8. **Run tests in CI on every PR** — no merge without green tests

---

*Testing plan complete. Cross-reference: [BUSINESSOS_AUDIT_REPORT.md](./BUSINESSOS_AUDIT_REPORT.md)*
