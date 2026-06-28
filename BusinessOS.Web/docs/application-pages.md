# BusinessOS Application Pages

> Complete inventory of every frontend page, route, purpose, components, and API dependencies.

**Legend:** ✅ Implemented · 🚧 Placeholder · 📋 Planned · ⚠️ Blocked on backend

---

## Layout Shells

| Layout | Route prefix | Components | Status |
|--------|--------------|------------|--------|
| Auth layout | `/auth/*` | Centered card, logo, footer links | ✅ |
| Dashboard layout | `/*` (authenticated) | Navbar, sidebar, breadcrumb, toast container, `<router-outlet>` | ✅ |

---

## Authentication Module

| Page | Route | Purpose | Key components | API | Status |
|------|-------|---------|----------------|-----|--------|
| Login | `/auth/login` | Sign in | Reactive form, remember-me (local) | `POST /auth/login` | ✅ |
| Register | `/auth/register` | Create account + business | Multi-field form, password strength | `POST /auth/register` | ✅ |
| Forgot password | `/auth/forgot-password` | Request reset link | Email field | `POST /auth/forgot-password` | 🚧 UI only |
| Reset password | `/auth/reset-password` | Set new password | Token + password fields | `POST /auth/reset-password` | 🚧 UI only |

---

## Onboarding Module 📋

| Page | Route | Purpose | API | Status |
|------|-------|---------|-----|--------|
| Wizard shell | `/onboarding` | Stepper container, progress, skip | — | 📋 |
| Welcome | `/onboarding/welcome` | Explain steps, estimated time | — | 📋 |
| Business info | `/onboarding/business` | Name, type, industry, owner, contact, address, tax # | Future tenant API | 📋 |
| Branding | `/onboarding/branding` | Logo upload preview | Future upload API | 📋 |
| Preferences | `/onboarding/settings` | Currency, timezone, language, tax % | Future settings API | 📋 |
| First category | `/onboarding/category` | Minimal category form | `POST /categories` | 📋 |
| First product | `/onboarding/product` | Simplified product form | `POST /products` | 📋 |
| First customer | `/onboarding/customer` | Simplified customer form | `POST /customers` | 📋 |
| First order | `/onboarding/order` | Guided order with pre-filled data | `POST /orders` | 📋 |
| Complete | `/onboarding/complete` | Success + link to dashboard | — | 📋 |

**Guard:** Redirect to `/onboarding` if `onboardingCompleted !== true` (localStorage until backend flag).

---

## Dashboard Module

| Page | Route | Purpose | Widgets | API | Status |
|------|-------|---------|---------|-----|--------|
| Overview | `/dashboard` | Business snapshot | Today/month sales, counts, low stock, pending orders, charts | `/dashboard/overview`, `/sales`, `/orders`, charts | ✅ extend |

**Planned widget additions:**
- Low stock table (link to `/inventory/alerts`)
- Pending orders list (link to `/orders?status=Pending`)
- Quick actions: New order, Add product, Receive stock

---

## Products Module 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Product list | `/products` | `Product.View` | `GET /products` | 🚧 |
| Product detail | `/products/:id` | `Product.View` | `GET /products/:id` | 📋 |
| Create product | `/products/new` | `Product.Create` | `POST /products` | 📋 |
| Edit product | `/products/:id/edit` | `Product.Update` | `PUT /products/:id` | 📋 |

**List page features:** Search, category filter, active/inactive toggle, sort, pagination, profit margin column, low stock badge.

**Detail page tabs:** Overview, Inventory (link), Order history (future).

**Form fields:** Category (dropdown), Name, SKU, Description, Cost price, Sale price, Reorder level, Active toggle.

**Disabled UI:** Image upload (tooltip: "Coming soon").

---

## Categories Submodule 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Category list | `/products/categories` | `Category.View` | `GET /categories` | 📋 |
| Create category | `/products/categories/new` | `Category.Create` | `POST /categories` | 📋 |
| Edit category | `/products/categories/:id/edit` | `Category.Update` | `PUT /categories/:id` | 📋 |

**Note:** Categories missing from sidebar — add under Products section per [navigation-structure.md](./navigation-structure.md).

---

## Customers Module 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Customer list | `/customers` | `Customer.View` | `GET /customers` | 🚧 |
| Customer detail | `/customers/:id` | `Customer.View` | `GET /customers/:id`, analytics, orders | 📋 |
| Create customer | `/customers/new` | `Customer.Create` | `POST /customers` | 📋 |
| Edit customer | `/customers/:id/edit` | `Customer.Update` | `PUT /customers/:id` | 📋 |

**Detail tabs:**
| Tab | Content | API |
|-----|---------|-----|
| Overview | Contact info, analytics KPIs | `/customers/:id/analytics` |
| Orders | Purchase history table | `/customers/:id/orders` |
| Notes | Free-text notes | ❌ No backend — localStorage or future |

**List filters:** Search, city, country, active status.

---

## Orders Module 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Order list | `/orders` | `Order.View` | `GET /orders` | 🚧 |
| Order detail | `/orders/:id` | `Order.View` | `GET /orders/:id` | 📋 |
| Create order | `/orders/new` | `Order.Create` | `POST /orders` | 📋 |
| Edit order | `/orders/:id/edit` | `Order.Update` | `PUT /orders/:id` | 📋 |

**Detail actions:** Status dropdown (`PATCH /status`), Print, Export PDF (client), Generate invoice (print view).

**List filters:** Status, search (order #, customer), date range (client-side until API supports).

**Create form sections:** Customer picker, Line items repeater, Discount/Tax, Summary panel.

---

## Inventory Module 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Inventory list | `/inventory` | `Inventory.View` | `GET /inventory` | 🚧 |
| Product stock detail | `/inventory/:productId` | `Inventory.View` | `GET /inventory/:productId` | 📋 |
| Threshold settings | `/inventory/:productId/settings` | `Inventory.Update` | `PUT /inventory/:productId` | 📋 |
| Receive stock | `/inventory/receive` | `Inventory.Adjust` | `POST /inventory/increase` | 📋 |
| Adjust stock | `/inventory/adjust` | `Inventory.Adjust` | `POST /inventory/adjust` | 📋 |
| Transaction history | `/inventory/transactions` | `Inventory.View` | `GET /inventory/transactions` | 📋 |
| Alerts | `/inventory/alerts` | `Inventory.View` | `/low-stock`, `/out-of-stock` | 📋 |
| Reorder suggestions | `/inventory/reorder` | `Inventory.View` | `GET /inventory/reorder-products` | 📋 |

---

## Invoices Module ⚠️

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Invoice list | `/invoices` | `Order.View` | ❌ Future `/invoices` | 🚧 |
| Invoice detail | `/invoices/:id` | `Order.View` | ❌ | 📋 |
| Create invoice | `/invoices/new` | `Order.Create` | ❌ | 📋 |

**Interim page:** `/orders/:id/invoice` — print-optimized order view labeled "Invoice".

---

## Reports Module 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Reports hub | `/reports` | Mixed | — | 🚧 |
| Sales report | `/reports/sales` | `Order.View` | `/dashboard/sales`, charts/revenue | 📋 |
| Inventory report | `/reports/inventory` | `Inventory.View` | `/dashboard/inventory` | 📋 |
| Customer report | `/reports/customers` | `Customer.View` | `/dashboard/customers` | 📋 |
| Product report | `/reports/products` | `Product.View` | `/dashboard/products` | 📋 |
| Order report | `/reports/orders` | `Order.View` | `/dashboard/orders` | 📋 |
| Profit report | `/reports/profit` | `Order.View` + `Product.View` | Computed client-side from sales + cost | 📋 |

**Shared components:** Date range picker (`period` param), export toolbar (Excel, CSV, PDF).

---

## Settings Module ⚠️

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Settings hub | `/settings` | Admin (implicit) | — | 🚧 |
| Business profile | `/settings/business` | Admin | ❌ Future tenant API | 📋 |
| Branding | `/settings/branding` | Admin | ❌ Logo upload | 📋 |
| Preferences | `/settings/preferences` | Admin | ❌ Currency, timezone, tax | 📋 |
| Invoice settings | `/settings/invoices` | Admin | ❌ Template, numbering | 📋 |

---

## Profile Module 📋

| Page | Route | Purpose | API | Status |
|------|-------|---------|-----|--------|
| User profile | `/profile` | View/edit name, email | ❌ Future user profile API | 🚧 |
| Change password | `/profile/password` | Password form | ❌ Future | 📋 |

---

## Admin Module 📋

| Page | Route | Permission | API | Status |
|------|-------|------------|-----|--------|
| Users list | `/users` | `User.View` | ❌ Missing list API | 🚧 |
| User detail | `/users/:id` | `User.View` | ❌ | 📋 |
| Assign roles | `/users/:id/roles` | `User.Update` | `POST/DELETE /users/:id/roles` | 📋 |
| Roles list | `/roles` | `Role.View` | `GET /roles` | 🚧 |
| Role detail | `/roles/:id` | `Role.View` | `GET /roles/:id` | 📋 |
| Create role | `/roles/new` | `Role.Create` | `POST /roles` | 📋 |
| Edit role | `/roles/:id/edit` | `Role.Update` | `PUT /roles/:id` | 📋 |
| Permission matrix | `/roles/:id/permissions` | `Role.Update` | Assign/remove permissions | 📋 |
| Permissions catalog | `/permissions` | `Role.View` | `GET /permissions` | 🚧 |

---

## System Pages 📋

| Page | Route | Purpose | Status |
|------|-------|---------|--------|
| 403 Forbidden | `/forbidden` | Permission denied | 📋 |
| 404 Not found | `/not-found` | Unknown route | 📋 |
| Session expired | `/auth/login?reason=expired` | Re-auth prompt | 📋 |

---

## Shared Page Patterns

Every list page includes:

```
┌─────────────────────────────────────────┐
│ Breadcrumb                              │
│ Page title + primary action button      │
├─────────────────────────────────────────┤
│ Search bar │ Filters │ Sort dropdown    │
├─────────────────────────────────────────┤
│ Data table / card grid                  │
│  - Loading: skeleton rows               │
│  - Empty: AppEmptyState + CTA           │
│  - Error: AppAlert + retry              │
├─────────────────────────────────────────┤
│ AppPagination                           │
└─────────────────────────────────────────┘
```

Every form page includes:

```
┌─────────────────────────────────────────┐
│ Breadcrumb                              │
│ Page title                              │
├─────────────────────────────────────────┤
│ Reactive form with inline validation    │
│ Help tooltips on complex fields         │
├─────────────────────────────────────────┤
│ Cancel │ Save (primary)                  │
└─────────────────────────────────────────┘
```

---

## Route Configuration Summary

| Module | Lazy load path | Route file |
|--------|----------------|------------|
| Auth | `features/auth/auth.routes.ts` | ✅ |
| Dashboard | `features/dashboard/dashboard.routes.ts` | ✅ |
| Onboarding | `features/onboarding/onboarding.routes.ts` | 📋 |
| Products | `features/products/products.routes.ts` | 📋 |
| Customers | `features/customers/customers.routes.ts` | 📋 |
| Orders | `features/orders/orders.routes.ts` | 📋 |
| Inventory | `features/inventory/inventory.routes.ts` | 📋 |
| Invoices | `features/invoices/invoices.routes.ts` | 📋 |
| Reports | `features/reports/reports.routes.ts` | 📋 |
| Settings | `features/settings/settings.routes.ts` | 📋 |
| Admin | `features/admin/admin.routes.ts` | 📋 |

**Total pages planned:** ~55  
**Implemented:** ~5 (auth × 4 + dashboard × 1)  
**Placeholder:** ~11 (feature-page component)
