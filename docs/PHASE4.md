# Phase 4 — Finance, Administration & Business Management

Phase 4 completes BusinessOS as a full Business Operating System. Business owners can manage expenses, finances, users, roles, audit trails, notifications, reports, and system settings from one platform.

## Architecture

### Backend (.NET 10)

| Layer | Pattern |
|-------|---------|
| **Domain** | Entities with tenant isolation (`TenantId`), soft delete (`IsDeleted`), audit fields |
| **Application** | MediatR CQRS for Expenses; service interfaces for Finance, Users, Audit, Notifications, Settings, System Admin |
| **Infrastructure** | EF Core implementations, Identity integration, RBAC seeding |
| **API** | Minimal API endpoint groups with `RequirePermission` guards |

### Frontend (Angular 19)

| Layer | Pattern |
|-------|---------|
| **Core** | Models, services extending `BaseApiService`, permission/route/API constants |
| **Features** | Lazy-loaded standalone components with signals + OnPush |
| **Shared** | Reusable UI components (breadcrumb, page-header, tables, charts, dialogs) |
| **Guards** | `permissionGuard` on routes; component-level permission checks for actions |

## Modules

### 1. Expense Management
- **Routes:** `/expenses`, `/expense-categories`
- **API:** `GET/POST/PUT/DELETE /api/expenses`, `GET /api/expenses/analytics`
- **Permissions:** `Expense.*`, `ExpenseCategory.*`
- **Features:** CRUD, recurring expenses, receipt URL, status workflow, analytics/trends

### 2. Financial Dashboard & P&L
- **Routes:** `/finance`, `/finance/profit-loss`
- **API:** `/api/finance/dashboard`, `/api/finance/profit-loss`, breakdown endpoints
- **Permissions:** `Finance.View`
- **Widgets:** Revenue, expenses, net profit, cash flow, receivables, trend charts
- **Export:** CSV/Excel via `ExportHelper`

### 3. User Management
- **Route:** `/users`
- **API:** Full CRUD at `/api/users` + activate/deactivate/reset-password
- **Permissions:** `User.*`
- **Features:** Invite/create users, role assignment, activity via audit logs

### 4. Role Management
- **Route:** `/roles`
- **API:** Existing `/api/roles` with permission matrix assignment
- **Permissions:** `Role.*`
- **System roles:** Admin, Manager, Accountant, Sales, InventoryManager, Viewer

### 5. Permission Management
- **Route:** `/permissions`
- **API:** `GET /api/permissions` (read-only catalog grouped by module)
- **UI:** Permission matrix grouped by category (Product, Order, Expense, Finance, etc.)

### 6. Audit Logs
- **Route:** `/audit`
- **API:** `GET /api/audit-logs` with filters
- **Permissions:** `Audit.View`
- **Tracks:** RBAC changes (who, what, when, old/new values)

### 7. Notifications
- **Routes:** `/notifications`, `/notifications/settings`
- **API:** `/api/notifications`, preferences endpoints
- **Permissions:** `Notification.View`, `Notification.Update`
- **Navbar:** Live notification dropdown wired to API

### 8. Reports Center
- **Route:** `/reports`
- **Tabs:** Customers, sales, invoices, payments, revenue, outstanding, expenses, suppliers, inventory, audit, users
- **Permissions:** `Report.View`
- **Features:** Export CSV/Excel, date filtering

### 9. Business Settings
- **Route:** `/settings`
- **API:** `GET/PUT /api/settings`
- **Permissions:** `Settings.View`, `Settings.Update`
- **Tabs:** General, business profile, currency/tax, invoice, email, notifications, theme, security, branding

### 10. System Administration
- **Route:** `/admin`
- **API:** `/api/system/health`, `/api/system/stats`, `/api/system/environment`
- **Permissions:** `SystemAdmin.View`

## Multi-Tenant Support

BusinessOS uses **shared-database, row-level tenant isolation**:
- Tenant resolved from JWT `TenantId` claim or `X-Tenant-ID` header
- All entities auto-scoped via EF global query filters
- **Business Settings** serves as the tenant configuration UI (profile, branding, currency)
- Full super-admin cross-tenant management is not implemented (by design for SaaS tenant isolation)

## Default Expense Categories

Seeded automatically on tenant registration:
Rent, Electricity, Internet, Transportation, Salary, Marketing, Office Supplies, Taxes, Maintenance, Miscellaneous

## Database Migration

Run migration `AddPhase4Modules` which adds:
- `ExpenseCategories`, `TenantSettings` tables
- Extended `Expenses` columns (category FK, payment method, vendor, status, recurring, receipt)
- Notification indexes and constraints

```bash
dotnet ef database update --project BusinessOS.Infrastructure --startup-project BusinessOS.API
```

## Permission Matrix (New Codes)

| Module | Codes |
|--------|-------|
| Expense | Create, View, Update, Delete |
| ExpenseCategory | Create, View, Update, Delete |
| Finance | View |
| Audit | View |
| Notification | View, Update |
| Report | View |
| Settings | View, Update |
| SystemAdmin | View |

## Testing

- **Backend:** `dotnet build`, `dotnet test`
- **Frontend:** `npm run build`, component/service specs in each feature folder
- **Manual:** Log in as Admin → verify Finance, Expenses, Users, Settings routes

## Key Files

| Area | Path |
|------|------|
| Expense API | `BusinessOS.API/Endpoints/ExpenseEndpoints.cs` |
| Finance service | `BusinessOS.Infrastructure/Services/FinanceService.cs` |
| User API | `BusinessOS.API/Endpoints/UserEndpoints.cs` |
| Permissions | `BusinessOS.Application/Common/Authorization/PermissionCodes.cs` |
| Frontend routes | `BusinessOS.Web/src/app/app.routes.ts` |
| Navigation | `BusinessOS.Web/src/app/shared/constants/nav.constants.ts` |
