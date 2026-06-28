# BusinessOS Permission Matrix

> Roles, permission codes, page access, and UI action visibility.

---

## 1. Authorization Model

| Layer | Mechanism |
|-------|-----------|
| Authentication | JWT Bearer token |
| Tenant isolation | `X-Tenant-ID` header + EF global filters |
| Authorization | Permission codes in JWT `perm` claim (comma-separated) |
| Route protection | `permissionGuard(['X.View'])` on Angular routes |
| UI protection | Hide buttons/links when permission missing |
| API enforcement | `[RequirePermission("X.Y")]` on Minimal API endpoints |

**Principle:** UI hiding is UX only — backend always enforces permissions.

---

## 2. Permission Catalog (28 permissions)

| Code | Name | Category |
|------|------|----------|
| `Category.Create` | Create Category | Category |
| `Category.View` | View Categories | Category |
| `Category.Update` | Update Category | Category |
| `Category.Delete` | Delete Category | Category |
| `Product.Create` | Create Product | Product |
| `Product.View` | View Products | Product |
| `Product.Update` | Update Product | Product |
| `Product.Delete` | Delete Product | Product |
| `Customer.Create` | Create Customer | Customer |
| `Customer.View` | View Customers | Customer |
| `Customer.Update` | Update Customer | Customer |
| `Customer.Delete` | Delete Customer | Customer |
| `Order.Create` | Create Order | Order |
| `Order.View` | View Orders | Order |
| `Order.Update` | Update Order | Order |
| `Order.Delete` | Delete Order | Order |
| `Inventory.View` | View Inventory | Inventory |
| `Inventory.Update` | Update Inventory | Inventory |
| `Inventory.Adjust` | Adjust Inventory | Inventory |
| `User.Create` | Create User | User |
| `User.View` | View Users | User |
| `User.Update` | Update User | User |
| `User.Delete` | Delete User | User |
| `Role.Create` | Create Role | Role |
| `Role.View` | View Roles | Role |
| `Role.Update` | Update Role | Role |
| `Role.Delete` | Delete Role | Role |

---

## 3. Seeded Roles

| Role | Typical user | Description |
|------|--------------|-------------|
| **Admin** | Business owner | Full access — all 28 permissions |
| **Manager** | Store manager | Operations: products, customers, orders, inventory |
| **Sales** | Cashier / sales rep | Customers and orders only |
| **InventoryManager** | Stock clerk | Products and inventory |
| **Viewer** | Accountant / auditor | Read-only across modules |

---

## 4. Role → Permission Matrix

| Permission | Admin | Manager | Sales | InventoryManager | Viewer |
|------------|:-----:|:-------:|:-----:|:----------------:|:------:|
| **Category.Create** | ✅ | — | — | — | — |
| **Category.View** | ✅ | — | — | — | ✅ |
| **Category.Update** | ✅ | — | — | — | — |
| **Category.Delete** | ✅ | — | — | — | — |
| **Product.Create** | ✅ | ✅ | — | ✅ | — |
| **Product.View** | ✅ | ✅ | — | ✅ | ✅ |
| **Product.Update** | ✅ | ✅ | — | ✅ | — |
| **Product.Delete** | ✅ | ✅ | — | ✅ | — |
| **Customer.Create** | ✅ | ✅ | ✅ | — | — |
| **Customer.View** | ✅ | ✅ | ✅ | — | ✅ |
| **Customer.Update** | ✅ | ✅ | ✅ | — | — |
| **Customer.Delete** | ✅ | ✅ | ✅ | — | — |
| **Order.Create** | ✅ | ✅ | ✅ | — | — |
| **Order.View** | ✅ | ✅ | ✅ | — | ✅ |
| **Order.Update** | ✅ | ✅ | ✅ | — | — |
| **Order.Delete** | ✅ | ✅ | ✅ | — | — |
| **Inventory.View** | ✅ | ✅ | — | ✅ | ✅ |
| **Inventory.Update** | ✅ | ✅ | — | ✅ | — |
| **Inventory.Adjust** | ✅ | ✅ | — | ✅ | — |
| **User.Create** | ✅ | — | — | — | — |
| **User.View** | ✅ | — | — | — | ✅ |
| **User.Update** | ✅ | — | — | — | — |
| **User.Delete** | ✅ | — | — | — | — |
| **Role.Create** | ✅ | — | — | — | — |
| **Role.View** | ✅ | — | — | — | ✅ |
| **Role.Update** | ✅ | — | — | — | — |
| **Role.Delete** | ✅ | — | — | — | — |

**Note:** Manager role does not include Category or User/Role permissions in backend seed. Admin must manage categories unless custom role created.

---

## 5. Page Access Matrix

| Page / Route | Required permission(s) | Guard mode |
|--------------|------------------------|------------|
| `/dashboard` | Authenticated | authGuard |
| `/products` | `Product.View` | permissionGuard |
| `/products/new` | `Product.Create` | component-level |
| `/products/:id/edit` | `Product.Update` | component-level |
| `/products/categories` | `Category.View` | permissionGuard |
| `/products/categories/new` | `Category.Create` | component-level |
| `/customers` | `Customer.View` | permissionGuard |
| `/customers/new` | `Customer.Create` | component-level |
| `/orders` | `Order.View` | permissionGuard |
| `/orders/new` | `Order.Create` | component-level |
| `/inventory` | `Inventory.View` | permissionGuard |
| `/inventory/receive` | `Inventory.Adjust` | component-level |
| `/inventory/adjust` | `Inventory.Adjust` | component-level |
| `/invoices` | `Order.View` | permissionGuard (interim) |
| `/reports` | `Order.View` | permissionGuard |
| `/reports/inventory` | `Inventory.View` | component-level |
| `/reports/customers` | `Customer.View` | component-level |
| `/reports/products` | `Product.View` | component-level |
| `/settings` | Authenticated (Admin recommended) | authGuard |
| `/users` | `User.View` | permissionGuard |
| `/roles` | `Role.View` | permissionGuard |
| `/permissions` | `Role.View` | permissionGuard |
| `/profile` | Authenticated | authGuard |
| `/onboarding` | Authenticated | authGuard |

**Guard mode `false`:** User needs **any one** of listed permissions (OR logic) — used in current `permissionGuard(item.permissions, false)`.

---

## 6. UI Action Matrix

| Action | Permission | Location |
|--------|------------|----------|
| "Add product" button | `Product.Create` | Products list |
| "Edit" row action | `Product.Update` | Products list |
| "Delete" row action | `Product.Delete` | Products list |
| "Add category" | `Category.Create` | Categories list |
| "New customer" | `Customer.Create` | Customers list |
| "New order" | `Order.Create` | Orders list, dashboard |
| Status dropdown | `Order.Update` | Order detail |
| "Cancel order" | `Order.Update` | Order detail |
| "Delete order" | `Order.Delete` | Order list |
| "Receive stock" | `Inventory.Adjust` | Inventory |
| "Adjust stock" | `Inventory.Adjust` | Inventory |
| Edit thresholds | `Inventory.Update` | Inventory detail |
| "Create role" | `Role.Create` | Roles list |
| Permission checkboxes | `Role.Update` | Role detail |
| "Assign role to user" | `User.Update` | User detail |
| Export report | Same as report view | Reports |

---

## 7. Dashboard Widget Visibility

| Widget | Minimum permission |
|--------|-------------------|
| Sales KPIs | `Order.View` |
| Revenue chart | `Order.View` |
| Customer count / chart | `Customer.View` |
| Product performance | `Product.View` |
| Inventory alerts | `Inventory.View` |
| Order status breakdown | `Order.View` |
| Quick action: New order | `Order.Create` |
| Quick action: Add product | `Product.Create` |

**Viewer role:** Sees dashboard widgets but no action buttons.

---

## 8. Helper Utilities (Frontend)

```typescript
// core/services/permission.service.ts (planned)
hasPermission(code: string): boolean;
hasAnyPermission(codes: string[]): boolean;
hasAllPermissions(codes: string[]): boolean;

// Template usage
@if (permissionService.hasPermission('Product.Create')) {
  <app-button>Add Product</app-button>
}
```

**Source of truth:** `AuthService.permissions()` signal from JWT decode at login.

---

## 9. 403 Handling

| Context | Behavior |
|---------|----------|
| Route guard | Redirect to `/forbidden` with message |
| API 403 | Toast: "You don't have permission to do this" |
| Missing nav item | Hidden entirely (current navbar behavior) |

**Forbidden page content:**
- "Access denied"
- "You don't have permission to view this page"
- Link back to dashboard
- Contact admin suggestion

---

## 10. Custom Roles

Admins can create custom roles via `/roles` and assign any combination of permissions.

**UI requirements:**
- Permission matrix grouped by category (Category, Product, Customer…)
- Select all in category checkbox
- Cannot delete system roles: Admin, Manager, Sales, InventoryManager, Viewer
- Show warning when removing own Admin role (future)

---

## 11. Registration Default

New registrations receive:
- Role: **Admin**
- Permissions: **All 28**

This ensures owner can complete onboarding without permission errors.

---

## 12. Known Gaps

| Permission | API exists? | Frontend impact |
|------------|-------------|-----------------|
| `User.Create` | ❌ | Hide "Invite user" until API |
| `User.View` | ❌ | Users page shows "Coming soon" |
| `User.Delete` | ❌ | No delete action |
| Category perms | ✅ | Manager role lacks them — Admin creates categories or custom role |

---

## 13. Testing Checklist

- [ ] Login as Viewer — no create/edit/delete buttons visible
- [ ] Login as Sales — can access orders/customers, not inventory nav
- [ ] Login as InventoryManager — inventory + products, not orders
- [ ] Direct URL to forbidden route — 403 page shown
- [ ] API call without permission — error toast, no crash
- [ ] Nav items match role permissions exactly
