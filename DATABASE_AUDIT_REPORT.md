# BusinessOS — Database Audit Report

**Date:** June 28, 2026  
**Database:** SQL Server (EF Core 10.0.9)  
**Auditor:** Enterprise Solution Architect / Senior .NET Developer

---

## Database Health Report

**Overall Score: 7.5 / 10**

BusinessOS uses a well-designed shared-database multi-tenant schema with comprehensive indexing on query hot paths, soft deletes, and proper FK constraints. The schema supports a full ERP workflow from products through orders to invoices and payments. Gaps exist in audit trail completeness, tenant-scoped RBAC, and several orphaned entities.

---

## 1. Entity Inventory

### Core Business Entities (Tenant-Scoped)

| Entity | Base Type | Soft Delete | Tenant Filter | API Layer |
|--------|-----------|:-----------:|:-------------:|:---------:|
| Tenant | AuditableEntity | ✅ | N/A (root) | ✅ |
| TenantSettings | AuditableEntity | ✅ | ✅ | ✅ |
| Category | AuditableEntity | ✅ | ✅ | ✅ |
| Product | AuditableEntity | ✅ | ✅ | ✅ |
| Customer | AuditableEntity | ✅ | ✅ | ✅ |
| Supplier | AuditableEntity | ✅ | ✅ | ✅ |
| Order | AuditableEntity | ✅ | ✅ | ✅ |
| OrderItem | BaseEntity | ✅ (manual) | ✅ | ✅ |
| Purchase | AuditableEntity | ✅ | ✅ | ✅ |
| PurchaseItem | BaseEntity | ✅ (manual) | ✅ | ✅ |
| Payment | AuditableEntity | ✅ | ✅ | ✅ |
| Invoice | AuditableEntity | ✅ | ✅ | ✅ |
| Quotation | AuditableEntity | ✅ | ✅ | ✅ |
| QuotationItem | BaseEntity | ✅ (manual) | ✅ | ✅ |
| Expense | AuditableEntity | ✅ | ✅ | ✅ |
| ExpenseCategory | AuditableEntity | ✅ | ✅ | ✅ |
| Inventory | AuditableEntity | ✅ | ✅ | ✅ |
| StockTransaction | AuditableEntity | ✅ | ✅ | ✅ |
| Notification | AuditableEntity | ✅ | ✅ | ✅ |
| Employee | AuditableEntity | ✅ | ✅ | ❌ No API |
| AIConversation | AuditableEntity | ✅ | ❌ **Missing filter** | ❌ No API |

### RBAC Entities (Global — Not Tenant-Scoped)

| Entity | Tenant Filter | Soft Delete |
|--------|:-------------:|:-----------:|
| Role | ❌ | ❌ |
| Permission | ❌ | ❌ |
| UserRole | ❌ | ❌ |
| RolePermission | ❌ | ❌ |
| RbacAuditLog | ❌ | ❌ |

### Identity (ASP.NET Identity Tables)

| Entity | TenantId | Notes |
|--------|:--------:|-------|
| ApplicationUser | ✅ | Extends IdentityUser |
| ApplicationRole | ❌ | Plain IdentityRole |

---

## 2. Relationship Map

```
Tenant ──1:1── TenantSettings
Tenant ──1:N── ApplicationUser
Tenant ──1:N── [All business entities]

Category ──1:N── Product ──1:1── Inventory
Product ──1:N── StockTransaction
Product ──1:N── OrderItem / PurchaseItem / QuotationItem

Customer ──1:N── Order ──1:N── OrderItem
Customer ──1:N── Quotation ──1:N── QuotationItem
Customer ──1:N── Invoice
Customer ──1:N── Payment

Order ──1:1── Invoice (unique constraint)
Order ──1:N── Payment

Supplier ──1:N── Purchase ──1:N── PurchaseItem

ExpenseCategory ──1:N── Expense

Role ──1:N── UserRole ──N:1── User
Role ──1:N── RolePermission ──N:1── Permission
```

**Delete behaviors:** `Restrict` on business FKs (prevents orphaned references); `Cascade` on TenantSettings → Tenant.

---

## 3. Indexes Analysis

### Well-Indexed Entities ✅

| Entity | Indexes | Purpose |
|--------|---------|---------|
| Product | `(TenantId, SKU)` unique, `(TenantId, CategoryId)`, `(TenantId, Name)`, `(TenantId, IsActive)` | Catalog search, filtering |
| Order | `(TenantId, OrderNumber)` unique, `(TenantId, CreatedAt)`, `(TenantId, Status)` | List, sort, filter |
| Customer | `(TenantId, Email)` unique, `(TenantId, CreatedAt)` | Lookup, list |
| Inventory | `(TenantId, ProductId)` unique, `(TenantId, CurrentStock)`, `(TenantId, ReorderLevel)` | Stock queries, alerts |
| StockTransaction | `(TenantId, ProductId, CreatedAt)` composite | History queries |
| Invoice | `(TenantId, InvoiceNumber)` unique, `(TenantId, OrderId)` unique | Lookup, dedup |
| Quotation | `(TenantId, QuotationNumber)` unique | Lookup |
| Supplier | `(TenantId, Email)` unique | Lookup |
| ExpenseCategory | `(TenantId, Name)` unique | Dedup |
| Notification | `(TenantId, UserId, IsRead)` | Unread count |
| Category | `(TenantId, Name)` unique filtered | Dedup |

### Missing Indexes ⚠

| Entity | Recommended Index | Reason |
|--------|-------------------|--------|
| Payment | `(TenantId, CustomerId, CreatedAt)` | Customer payment history |
| Payment | `(TenantId, OrderId)` | Order payment lookup |
| Expense | `(TenantId, ExpenseCategoryId, CreatedAt)` | Category expense reports |
| Expense | `(TenantId, Status, CreatedAt)` | Expense approval workflow |
| Purchase | `(TenantId, PurchaseDate)` | Date-range queries (may exist) |
| Invoice | `(TenantId, Status, CreatedAt)` | Overdue invoice queries |
| Quotation | `(TenantId, Status, ValidUntil)` | Expiring quotation alerts |
| ApplicationUser | `(TenantId, IsActive)` | Active user listing |
| OrderItem | `(TenantId, ProductId)` | Product sales analytics |
| Tenant | `(SubscriptionPlan, IsActive)` | SaaS admin queries |

---

## 4. Constraints Assessment

| Constraint Type | Status | Notes |
|-----------------|:------:|-------|
| Primary keys (Guid) | ✅ | All entities |
| Foreign keys | ✅ | Restrict on business entities |
| Unique constraints | ✅ | SKU, email, order/invoice numbers per tenant |
| Check constraints | ❌ | No DB-level validation (e.g., price > 0, stock >= 0) |
| Default values | ✅ | CreatedAt defaults to UtcNow |
| NOT NULL | ✅ | Required fields enforced via EF config |

**Recommendation:** Add check constraints for business rules (non-negative prices, stock quantities) as defense-in-depth.

---

## 5. Query Performance Risks

| Risk | Severity | Detail |
|------|:--------:|--------|
| N+1 queries in order/invoice detail | Medium | Handlers may `.Include()` multiple levels — verify projection usage |
| Dashboard aggregation queries | Medium | 8 parallel queries per dashboard load; mitigated by 5-min cache |
| Client-side pagination breaks | High | Frontend filters after fetch — wastes DB round trips |
| Missing composite indexes on Payment/Expense | Medium | Will degrade as data grows |
| Global query filter overhead | Low | EF adds WHERE clause to every query — acceptable with indexes |
| No query timeout configuration | Low | Long-running queries could block connections |
| Soft delete filter on every query | Low | Indexed `IsDeleted` columns mitigate |

---

## 6. Normalization Assessment

**Score: 8/10 — Well normalized**

| Area | Assessment |
|------|------------|
| Product catalog | ✅ 3NF — Category → Product separation |
| Order structure | ✅ Header + line items |
| Customer data | ✅ No redundant denormalization |
| Invoice | ✅ References Order + Customer (acceptable denormalization for immutability) |
| TenantSettings | ✅ Separate from Tenant (1:1) |
| RBAC | ✅ Proper M:N via junction tables |

**Minor denormalization (acceptable):**
- Order stores customer reference (not duplicated customer data)
- Invoice stores order reference for immutability after order changes

---

## 7. Migration Strategy

### Current State

| Migration | Date | Purpose |
|-----------|------|---------|
| InitialCreate | 2026-06-22 | Full schema bootstrap |
| SyncModelChanges | 2026-06-28 | Model sync |
| AddQueryPerformanceIndexes | 2026-06-28 | Performance indexes |
| AddOrderQueryIndexes | 2026-06-28 | Order-specific indexes |
| UpdateCustomerModel | 2026-06-28 | Customer field updates |
| AddInventoryModule | 2026-06-28 | Inventory + stock transactions |
| AddRbacModule | 2026-06-28 | RBAC tables |
| AddSupplierAndPurchaseOrderFields | 2026-06-28 | Supplier/PO enhancements |
| AddPhase4Modules | 2026-06-28 | Expenses, notifications, settings |

**Auto-migrate on startup:** `DbInitializer.MigrateWithRetryAsync` (5 retries with backoff). Skipped in `Testing` environment.

### Migration Concerns

| Issue | Severity | Recommendation |
|-------|:--------:|----------------|
| Auto-migrate in production | Medium | Use CI/CD pipeline migration step instead |
| No rollback strategy documented | Medium | Document down-migration procedures |
| No seed data versioning | Low | Version RBAC seed to handle updates |
| 9 migrations in 6 days | Low | Normal for active development; squash before v1.0 |

---

## 8. Audit Fields

| Field | Status | Auto-Managed |
|-------|:------:|:------------:|
| `CreatedAt` | ✅ All AuditableEntity | ✅ Default UtcNow |
| `UpdatedAt` | ✅ All AuditableEntity | ❌ Manual in handlers only |
| `CreatedBy` | ❌ Not implemented | — |
| `UpdatedBy` | ❌ Not implemented | — |
| `IsDeleted` | ✅ Soft delete | ✅ Auto on Delete() |
| `DeletedAt` | ❌ Not implemented | — |
| `DeletedBy` | ❌ Not implemented | — |

**Recommendation:** Override `SaveChangesAsync` to auto-stamp `UpdatedAt`, `CreatedBy`, `UpdatedBy` from current user context.

---

## 9. Soft Deletes

**Implementation:** `AuditableEntity.IsDeleted` + EF global query filter + `SaveChangesAsync` converts hard deletes to soft deletes.

| Entity | Soft Delete | Filter Applied |
|--------|:-----------:|:--------------:|
| All AuditableEntity types | ✅ | ✅ |
| OrderItem, PurchaseItem, QuotationItem | ✅ (manual field) | ✅ |
| RBAC entities | ❌ Hard delete | N/A |
| Identity tables | ❌ Hard delete | N/A |

**Gap:** No purge mechanism for GDPR compliance (permanent deletion after retention period).

---

## 10. Multi-Tenancy

### Current Model: Shared Database, Shared Schema, Row-Level Isolation

| Component | Implementation | Status |
|-----------|---------------|:------:|
| TenantId column | On all business entities | ✅ |
| EF global query filters | `TenantId == _tenantId && !IsDeleted` | ✅ |
| Auto TenantId on insert | SaveChangesAsync override | ✅ |
| Tenant middleware | JWT claim or X-Tenant-ID header | ✅ |
| ITenantDbConnection | Returns same connection for all tenants | ⚠ Misleading interface name |
| Database-per-tenant | Not implemented | ❌ |
| Schema-per-tenant | Not implemented | ❌ |

### Multi-Tenancy Risks

| Risk | Severity | Mitigation |
|------|:--------:|------------|
| Missing query filter (AIConversation) | High | Add filter immediately |
| RBAC global (not tenant-scoped) | High | Add TenantId to Role or use templates |
| User IDOR bypasses tenant isolation | Critical | Fix IdentityService |
| No tenant data export | Medium | Required for GDPR/offboarding |
| No tenant suspension mechanism | Medium | Add IsActive flag enforcement |
| Connection string same for all tenants | Low | Acceptable for shared DB model |

---

## 11. Optimization Recommendations

### Immediate (P0)

1. Add tenant query filter for `AIConversation`
2. Add missing indexes on Payment, Expense, Invoice status columns
3. Auto-stamp `UpdatedAt` in `SaveChangesAsync`

### Short-Term (P1)

4. Add `CreatedBy`/`UpdatedBy` columns to `AuditableEntity`
5. Add check constraints for non-negative prices and stock
6. Implement entity-level audit log table
7. Add tenant-scoped RBAC (TenantId on Role)
8. Remove or implement `Employee` entity

### Medium-Term (P2)

9. Add database query timeout configuration
10. Implement read replica support for reporting queries
11. Add table partitioning strategy for high-volume tenants (StockTransaction, Order)
12. Implement tenant data export/purge for GDPR
13. Move auto-migrate to CI/CD pipeline
14. Add database connection pooling configuration for production

### Long-Term (P3)

15. Evaluate schema-per-tenant for enterprise customers
16. Implement event sourcing for audit trail
17. Add full-text search indexes for product/customer search

---

## 12. Performance Risks Summary

| Risk | Impact at 1K tenants | Impact at 10K tenants |
|------|:--------------------:|:---------------------:|
| Missing Payment/Expense indexes | Low | Medium |
| Dashboard cache (in-memory only) | Medium | High — won't scale horizontally |
| Soft delete filter on all queries | Low | Low (with indexes) |
| No connection pooling tuning | Low | Medium |
| StockTransaction table growth | Low | High — needs partitioning |
| RBAC global tables | Low | Low (small table) |

---

*Database audit complete. Cross-reference: [BUSINESSOS_AUDIT_REPORT.md](./BUSINESSOS_AUDIT_REPORT.md)*
