# BusinessOS Frontend Roadmap

> **Status:** Planning complete — awaiting approval before implementation  
> **Stack:** Angular 19.2.15 · Bootstrap 5 · SCSS · Signals · Standalone Components  
> **Last analyzed:** June 28, 2026  
> **Backend:** 58 Minimal API endpoints across 8 domain groups

---

## 1. Executive Summary

BusinessOS is a multi-tenant Business Operating System for shop owners, retailers, wholesalers, distributors, and SMBs migrating from notebooks, Excel, WhatsApp, and paper invoices.

The **backend is partially complete**. Core operational modules (Products, Categories, Customers, Orders, Inventory, Dashboard analytics, RBAC) are API-ready. Several modules the business requires—**Invoices, Settings, User management, Reports export, Email verification, Password reset**—exist as domain entities or frontend routes but **lack API support**.

The frontend strategy is:

1. Ship value on **existing APIs first** (Dashboard, Products, Customers, Orders, Inventory, RBAC).
2. Design **Invoices, Settings, and Reports** UI against planned contracts, flagging backend gaps.
3. Guide non-technical users with **onboarding wizard**, empty states, and progressive disclosure.
4. Match the polish of Shopify / Zoho / Odoo while staying simpler than a full ERP.

---

## 2. Target Users & Jobs-to-be-Done

| Persona | Primary jobs | Success metric |
|---------|--------------|----------------|
| **Shop owner** | See daily sales, unpaid orders, low stock | Answers "How am I doing today?" in < 30 seconds |
| **Store cashier / sales staff** | Create orders, find customers | Completes sale in < 2 minutes |
| **Inventory clerk** | Receive stock, adjust counts | Stock matches physical shelf |
| **Admin / owner** | Add products, manage team access | Business fully digitized in first week |
| **Viewer (accountant)** | Read reports, export data | Gets numbers without changing data |

---

## 3. Backend Capability Matrix

| Module | API Status | Frontend Priority | Notes |
|--------|-----------|-------------------|-------|
| Authentication | ✅ Register, Login | P0 — enhance | No forgot/reset password, no email verify |
| Dashboard / Analytics | ✅ 11 endpoints | P0 — done (extend) | Covers most "Reports" needs |
| Categories | ✅ Full CRUD | P1 | Missing from sidebar today |
| Products | ✅ Full CRUD | P1 | No image upload field |
| Customers | ✅ Full CRUD + analytics | P1 | No outstanding balance field |
| Orders | ✅ Full CRUD + status FSM | P1 | Invoice generation not wired |
| Inventory | ✅ Full adjust + history | P1 | Transfer = transaction type only |
| RBAC (Roles/Permissions) | ✅ Roles + partial Users | P2 | User list/create/delete missing |
| Invoices / Payments | ❌ Entity only | P2 — UI shell | Blocked on backend |
| Settings / Tenant profile | ❌ Register only | P2 — UI shell | Tenant entity has fields, no update API |
| Suppliers / Purchases | ❌ Entity only | P3 | Future procurement module |
| Expenses / Employees | ❌ Entity only | P4 | Future HR/finance module |
| Notifications | ❌ Entity only | P4 | Future |

---

## 4. Frontend Current State

### Implemented
- Auth: Login, Register, Forgot/Reset password (UI only; reset APIs missing)
- Dashboard: Full page wired to `/api/dashboard/*`
- Core: JWT interceptor, `X-Tenant-ID`, auth + permission guards
- Shared UI: Navbar, sidebar, breadcrumb, card, chart, pagination, badge, alert, toast, skeleton, button, input
- Layouts: `auth-layout`, `dashboard-layout`

### Placeholder routes (no feature implementation)
Users, Roles, Permissions, Customers, Products, Inventory, Orders, Invoices, Reports, Settings, Profile

---

## 5. Module Roadmap

### Phase 0 — Foundation (Week 1)
**Goal:** Shared patterns every module reuses.

| Deliverable | Details |
|-------------|---------|
| API service layer | `BaseApiService` extensions per domain |
| TypeScript models | Mirror all backend DTOs |
| List page pattern | Search, filter, sort, pagination, empty/loading/error |
| Form page pattern | Reactive forms, validation messages matching FluentValidation |
| Confirm dialog service | Delete, cancel order, status change |
| Toast + error mapper | RFC 7807 Problem Details → user-friendly text |
| Breadcrumb service | Auto from route data |
| Categories nav item | Add to sidebar under Products |

### Phase 1 — Core Operations (Weeks 2–4)
**Goal:** Owner can run daily business on existing APIs.

| Module | Pages | Backend |
|--------|-------|---------|
| **Products** | List, Create, Edit, Detail, Categories sub-module | ✅ |
| **Customers** | List, Create, Edit, Detail (orders + analytics tabs) | ✅ |
| **Orders** | List, Create, Edit, Detail, Status workflow | ✅ |
| **Inventory** | List, Detail, Adjust, Transactions, Low/Out alerts | ✅ |

### Phase 2 — Onboarding & Settings (Week 5)
**Goal:** First-time user guided setup.

| Module | Pages | Backend |
|--------|-------|---------|
| **Onboarding wizard** | 7-step guided flow post-register | ⚠️ Partial — extend register + need Settings API |
| **Settings** | Business profile, preferences UI | ❌ Needs `PUT /api/tenants/{id}` |
| **Profile** | User name, password change | ❌ Needs User profile API |

### Phase 3 — Admin & Access (Week 6)
**Goal:** Team management for growing businesses.

| Module | Pages | Backend |
|--------|-------|---------|
| **Roles** | List, Create, Edit, Permission matrix | ✅ |
| **Permissions** | Read-only catalog | ✅ |
| **Users** | List, invite, assign roles | ⚠️ Role assign only today |

### Phase 4 — Invoices & Reports (Weeks 7–8)
**Goal:** Billing and export workflows.

| Module | Pages | Backend |
|--------|-------|---------|
| **Invoices** | List, Detail, Print view | ❌ Use Order as interim "proforma" |
| **Reports** | Sales, Inventory, Customer, Profit dashboards + export | ⚠️ Dashboard APIs cover analytics; export client-side |

### Phase 5 — Quality & Polish (Week 9)
**Goal:** Production readiness.

- Unit tests (Jasmine/Karma) per service + critical components
- Playwright E2E: auth, onboarding, create product → order flow
- Performance: lazy routes verified, skeleton loaders
- Accessibility: focus traps, ARIA on dialogs
- Responsive pass on all modules

---

## 6. UX Principles (Non-Negotiable)

Every screen must include:

| State | Pattern |
|-------|---------|
| **Empty** | Illustration + primary CTA + help text ("Add your first product") |
| **Loading** | Skeleton rows/cards, not spinners alone |
| **Error** | Inline field errors + page-level alert with retry |
| **Success** | Toast confirmation after save/delete |
| **Destructive** | Modal confirmation with consequence text |
| **Help** | Tooltips on jargon (SKU, reorder level, grand total) |

**Language:** Plain business English. Avoid "DTO", "tenant", "RBAC" in UI — use "Business", "Team access", "Your store".

---

## 7. Technical Architecture

```
BusinessOS.Web/src/app/
├── core/           # Singletons: auth, interceptors, guards, base API
├── shared/         # Dumb UI components, layouts, pipes
├── features/       # Lazy-loaded domain modules
│   ├── auth/
│   ├── dashboard/          ✅ implemented
│   ├── onboarding/       📋 planned
│   ├── products/
│   ├── customers/
│   ├── orders/
│   ├── inventory/
│   ├── invoices/           ⚠️ backend gap
│   ├── reports/
│   ├── settings/
│   └── admin/              roles, permissions, users
├── state/          # Signal-based feature state (dashboard pattern)
└── models/         # Shared TS interfaces
```

### Cross-cutting concerns
- **Auth:** JWT in localStorage; interceptor adds `Authorization` + `X-Tenant-ID`
- **Permissions:** Nav + route guards filter by JWT `permissions` claim
- **Pagination:** Standard query params `page`, `pageSize`, `search`, `sortBy`, `sortOrder`
- **Forms:** Reactive Forms with validators mirroring backend FluentValidation rules
- **State:** Angular Signals (`signal`, `computed`, `effect`) — follow `DashboardStateService` pattern

---

## 8. Backend Gaps — Frontend Handling Strategy

| Gap | Frontend approach until API exists |
|-----|----------------------------------|
| No Invoice API | "Generate invoice" opens printable Order detail; mark Invoices nav as Beta |
| No Settings API | Wizard saves to localStorage draft; show "Sync pending" banner |
| No User CRUD | Users page shows role-assignment for known user IDs only; hide Create until API |
| No forgot password | Keep UI; show "Contact admin" when 404 |
| No product images | Image upload UI disabled with "Coming soon" tooltip |
| No customer balance | Show computed "Total spending" from analytics API |
| No email verification | Skip verify step; document in onboarding as future |

---

## 9. Dashboard Widget Plan

Maps business questions → data sources:

| Widget | Question | API |
|--------|----------|-----|
| Today sales | How much did I sell today? | `GET /dashboard/sales` → `todaySales` |
| Month sales | How much this month? | `GET /dashboard/sales` → `monthlySales` |
| Customer count | How many customers? | `GET /dashboard/overview` → `totalCustomers` |
| Product count | How many products? | `GET /dashboard/overview` → `totalProducts` |
| Low stock alert | What's running low? | `GET /inventory/low-stock` or overview → `lowStockProducts` |
| Unpaid invoices | Which invoices unpaid? | ⚠️ No invoice API — show pending/completed orders instead |
| Pending orders | Which orders pending? | `GET /dashboard/orders` → `ordersByStatus` |
| Revenue chart | Trend over time | `GET /dashboard/charts/revenue` |

---

## 10. Related Planning Documents

| Document | Purpose |
|----------|---------|
| [user-flows.md](./user-flows.md) | End-to-end user journeys with diagrams |
| [api-mapping.md](./api-mapping.md) | Every endpoint → page mapping |
| [application-pages.md](./application-pages.md) | Full page inventory |
| [navigation-structure.md](./navigation-structure.md) | Sidebar, navbar, routing hierarchy |
| [database-entity-ui-mapping.md](./database-entity-ui-mapping.md) | Entity fields → form fields |
| [onboarding-flow.md](./onboarding-flow.md) | First-run wizard specification |
| [permission-matrix.md](./permission-matrix.md) | Roles × permissions × pages |
| [responsive-design-guide.md](./responsive-design-guide.md) | Breakpoints and layout rules |
| [implementation-plan.md](./implementation-plan.md) | Sprint-by-sprint execution plan |

---

## 11. Approval Checklist

Before writing feature code, confirm:

- [ ] Module priority order (Phase 1–5) approved
- [ ] Backend gap handling strategy accepted (Invoice/Settings defer vs. build UI shells)
- [ ] Onboarding scope agreed (minimal vs. full business profile)
- [ ] Permission model matches operational roles (Admin, Manager, Sales, InventoryManager, Viewer)
- [ ] Reports scope: dashboard-only vs. dedicated export pages

**Next step after approval:** Execute [implementation-plan.md](./implementation-plan.md) starting with Phase 0.
