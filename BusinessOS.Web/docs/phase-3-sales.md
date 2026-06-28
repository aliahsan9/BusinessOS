# Phase 3 — Sales Management System

Phase 3 delivers the complete customer-to-payment sales journey: customers, quotations, sales orders, invoices, payments, dashboards, and reports.

## Architecture Decisions

### Backend (CQRS + Minimal API)

New modules follow the same patterns as Orders and Purchase Orders:

| Module | Route | Permissions |
|--------|-------|-------------|
| Customers | `/api/customers` | `Customer.*` (existing) |
| Orders (Sales) | `/api/orders` | `Order.*` (existing) |
| Quotations | `/api/quotations` | `Quotation.*` |
| Invoices | `/api/invoices` | `Invoice.*` |
| Payments | `/api/payments` | `Payment.*` |
| Dashboard | `/api/dashboard/*` | Domain permissions (existing) |

**Sales workflow:**

```
Quotation (Draft → Sent → Accepted)
    ↓ convert-to-order
Order (Pending → Confirmed → Processing → Completed)
    ↓ create-from-order
Invoice (Draft → Sent → Paid / PartiallyPaid / Overdue)
    ↓ record payment
Payment (linked to Order + Customer)
```

**Key rules:**
- Quotations do not affect inventory until converted to an order (Accepted status required).
- Orders deduct inventory on creation; cancellation restores stock.
- One invoice per completed order (unique `OrderId` constraint).
- Invoice `AmountPaid` / `OutstandingAmount` are computed from payments on read.
- Payment methods: Cash, BankTransfer, CreditCard, DebitCard, Cheque, OnlinePayment.

### Frontend (Angular 19)

```
features/
├── customers/       # List, form, 360° detail (analytics + order history)
├── orders/          # Sales order CRUD + status workflow
├── quotations/      # Quote CRUD + convert-to-order
├── invoices/        # List, detail, print, status updates
├── payments/        # Record and track payments
├── sales/           # Sales KPI dashboard
└── reports/         # Multi-tab reports with CSV/Excel export

core/
├── models/          # customer, order, quotation, invoice, payment DTOs
├── services/        # BaseApiService extensions
└── enums/           # OrderStatus, InvoiceStatus, QuotationStatus, PaymentMethod
```

**Patterns (consistent with Phase 2):**
- Standalone components with `ChangeDetectionStrategy.OnPush`
- Component-local signals (Suppliers/PO pattern — no NgRx)
- Reactive forms with shared `app-input`, `app-card`, validation helpers
- Lazy-loaded routes with `permissionGuard`
- Permission-gated action buttons via `TokenService`

## Customer Management

| Page | Route | API |
|------|-------|-----|
| List | `/customers` | `GET /api/customers` |
| Create | `/customers/new` | `POST /api/customers` |
| Detail | `/customers/:id` | `GET /api/customers/{id}`, `/analytics`, `/orders` |
| Edit | `/customers/:id/edit` | `PUT /api/customers/{id}` |

**Backend fields:** firstName, lastName, email, phoneNumber, address, city, country, postalCode, isActive.

**Detail page widgets:** lifetime revenue (totalSpending), order count, average order value, last order date, paginated order history.

## Sales Orders

| Page | Route | API |
|------|-------|-----|
| List | `/orders` | `GET /api/orders` |
| Create | `/orders/new` | `POST /api/orders` |
| Detail | `/orders/:id` | `GET /api/orders/{id}`, `PATCH .../status` |
| Edit | `/orders/:id/edit` | `PUT /api/orders/{id}` |

Status workflow: Pending → Confirmed → Processing → Completed | Cancelled.

Completed orders expose **Convert to Invoice** on the detail page.

## Quotations

| Page | Route | API |
|------|-------|-----|
| List | `/quotations` | `GET /api/quotations` |
| Create | `/quotations/new` | `POST /api/quotations` |
| Detail | `/quotations/:id` | `GET`, `PATCH .../status`, `POST .../convert-to-order` |
| Edit | `/quotations/:id/edit` | `PUT /api/quotations/{id}` |

Status workflow: Draft → Sent → Accepted | Rejected | Expired.

## Invoices

| Page | Route | API |
|------|-------|-----|
| List | `/invoices` | `GET /api/invoices` |
| Detail | `/invoices/:id` | `GET`, `PATCH .../status`, `GET .../pdf` |

Invoices are created from completed orders (`POST /api/invoices/from-order/{orderId}`), not via a standalone form.

Print uses the authenticated HTML PDF endpoint opened in a new window.

## Payments

| Page | Route | API |
|------|-------|-----|
| List | `/payments` | `GET /api/payments` |
| Record | `/payments/new` | `POST /api/payments` |
| Detail | `/payments/:id` | `GET /api/payments/{id}` |
| Edit | `/payments/:id/edit` | `PUT /api/payments/{id}` |

## Dashboards & Reports

| Page | Route | Data sources |
|------|-------|--------------|
| Main Dashboard | `/dashboard` | Existing overview (unchanged) |
| Sales Dashboard | `/sales` | `/api/dashboard/sales`, orders, invoices, charts |
| Reports | `/reports` | Customers, orders, invoices, payments + export |

Reports support CSV, Excel (CSV-based), and print-friendly layouts via `ExportHelper`.

## Permissions

Sales role preset includes: Customer.*, Order.*, Quotation.*, Invoice.*, Payment.*.

Nav items are filtered by `TokenService.hasAnyPermission()`.

## Database Migration

New tables require an EF migration before use:

```bash
dotnet ef migrations add Phase3SalesModules --project BusinessOS.Infrastructure --startup-project BusinessOS.API
dotnet ef database update --project BusinessOS.Infrastructure --startup-project BusinessOS.API
```

Tables: `Invoices`, `Quotations`, `QuotationItems` (Payments table may already exist).

## Testing

- **Frontend:** 88 Karma unit tests (component smoke + service HTTP mocks)
- **Backend:** Run `dotnet test` for existing unit/integration test suites

## UX — Key Questions Answered

| Question | Where |
|----------|-------|
| Who owes me money? | Invoices list (Outstanding column), Sales Dashboard |
| Which invoices are overdue? | Invoices list filter, Reports → Outstanding |
| Top customers? | Sales Dashboard, Reports → Customers |
| Revenue today? | Sales Dashboard, Main Dashboard |
| Outstanding balance? | Invoice detail, Customer analytics |
| Top products? | Sales Dashboard chart |
