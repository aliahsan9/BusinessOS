# BusinessOS API Mapping

> Complete mapping of backend Minimal API endpoints to frontend pages, DTOs, validations, and permissions.  
> Base URL: `{environment.apiUrl}/api` (default `/api`)

---

## Global Conventions

### Authentication headers (all protected routes)
```
Authorization: Bearer {jwt}
X-Tenant-ID: {tenantId}
```

### Pagination query parameters
| Param | Default | Rules |
|-------|---------|-------|
| `page` | 1 | ≥ 1 |
| `pageSize` | 20 | 1–100 |
| `search` | — | Optional text search |
| `sortBy` | entity-specific | See per-endpoint |
| `sortOrder` / `sortDirection` | `asc` | `asc` or `desc` |

### Paged response (`PagedResult<T>`)
```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### Error response (RFC 7807)
HTTP 400 validation errors include `errors: { "fieldName": ["message"] }`.

---

## 1. Authentication

| Method | Route | Purpose | Permission |
|--------|-------|---------|------------|
| POST | `/auth/register` | Create user + tenant + JWT | Public |
| POST | `/auth/login` | Authenticate + JWT | Public |

### POST `/auth/register`

| | |
|---|---|
| **Purpose** | Register owner account; creates Tenant, ApplicationUser, assigns Admin role with all permissions |
| **Frontend page** | `/auth/register` |
| **Request DTO** | `RegisterCommand` |
| **Response DTO** | `AuthResponse` (201) |

**Request fields:**
| Field | Type | Validation |
|-------|------|------------|
| `email` | string | Required, valid email, max 256 |
| `password` | string | Required, min 8 chars |
| `firstName` | string | Required, max 100 |
| `lastName` | string | Required, max 100 |
| `businessName` | string | Required, max 200 |

**Response fields:**
| Field | Type |
|-------|------|
| `token` | string |
| `userId` | string |
| `email` | string |
| `tenantId` | guid |
| `roles` | string[] |
| `permissions` | string[] |
| `expiresAt` | datetime |

**Business rules:** Email must be unique (409). Redirect to onboarding wizard after success.

---

### POST `/auth/login`

| | |
|---|---|
| **Purpose** | Validate credentials; return JWT with roles and permissions |
| **Frontend page** | `/auth/login` |
| **Request DTO** | `LoginCommand` |
| **Response DTO** | `AuthResponse` (200) |

**Request fields:**
| Field | Type | Validation |
|-------|------|------------|
| `email` | string | Required, valid email |
| `password` | string | Required |

---

### ⚠️ Not implemented (frontend UI exists)

| Method | Route | Frontend page | Status |
|--------|-------|---------------|--------|
| POST | `/auth/forgot-password` | `/auth/forgot-password` | 404 — show fallback message |
| POST | `/auth/reset-password` | `/auth/reset-password` | 404 |
| POST | `/auth/verify-email` | Onboarding step 2 | Not planned in backend |

---

## 2. Categories

| Permission prefix | `Category.*` |
|-------------------|----------------|

### POST `/categories`
| | |
|---|---|
| **Purpose** | Create product category |
| **Frontend page** | `/products/categories/new` |
| **Permission** | `Category.Create` |
| **Request** | `{ name, description? }` |
| **Response** | `{ id }` (201) |

**Validation:** `name` required, max 200; `description` max 1000.

**Business rules:** Unique name per tenant (409).

---

### GET `/categories`
| | |
|---|---|
| **Purpose** | Paginated category list |
| **Frontend page** | `/products/categories` |
| **Permission** | `Category.View` |
| **Query** | `search`, `page`, `pageSize`, `sortBy` (name, description), `sortOrder` |
| **Response** | `PagedResult<CategoryDto>` |

**CategoryDto:** `id`, `name`, `description`

---

### GET `/categories/{id}`
| **Purpose** | Category detail / edit preload |
| **Frontend page** | `/products/categories/:id` |
| **Permission** | `Category.View` |
| **Response** | `CategoryDto` |

---

### PUT `/categories/{id}`
| **Purpose** | Update category |
| **Frontend page** | `/products/categories/:id/edit` |
| **Permission** | `Category.Update` |
| **Request** | `{ name, description? }` |
| **Response** | 204 |

---

### DELETE `/categories/{id}`
| **Purpose** | Delete category |
| **Frontend page** | Categories list (confirm dialog) |
| **Permission** | `Category.Delete` |
| **Response** | 204 |
| **Business rules** | Cannot delete if products exist (409) |

---

## 3. Products

| Permission prefix | `Product.*` |

### POST `/products`
| | |
|---|---|
| **Purpose** | Create product; auto-creates Inventory record |
| **Frontend page** | `/products/new` |
| **Permission** | `Product.Create` |
| **Request** | See below |
| **Response** | `{ id }` (201) |

**Request fields:**
| Field | Type | Validation |
|-------|------|------------|
| `categoryId` | guid | Required |
| `name` | string | Required, max 200 |
| `sku` | string | Required, max 50 |
| `description` | string? | Max 2000 |
| `costPrice` | decimal | > 0 |
| `salePrice` | decimal | > 0 |
| `reorderLevel` | decimal | ≥ 0 |

---

### GET `/products`
| | |
|---|---|
| **Purpose** | Paginated product catalog |
| **Frontend page** | `/products` |
| **Permission** | `Product.View` |
| **Query** | `categoryId?`, `search`, `page`, `pageSize`, `sortBy` (name, sku, salePrice, costPrice, currentStock, isActive), `sortDirection` |
| **Response** | `PagedResult<ProductDto>` |

**ProductDto:** `id`, `categoryId`, `name`, `sku`, `description`, `costPrice`, `salePrice`, `currentStock`, `reorderLevel`, `isActive`

**UI computed fields:** `profitMargin = salePrice - costPrice`, `marginPercent = (margin / salePrice) * 100`

---

### GET `/products/{id}`
| **Frontend page** | `/products/:id` |
| **Permission** | `Product.View` |
| **Response** | `ProductDto` |

---

### GET `/products/by-category/{categoryId}`
| **Frontend page** | `/products?categoryId=` filter |
| **Permission** | `Product.View` |
| **Response** | `PagedResult<ProductDto>` |

---

### PUT `/products/{id}`
| **Frontend page** | `/products/:id/edit` |
| **Permission** | `Product.Update` |
| **Request** | Create fields + `isActive` |
| **Response** | 204 |

---

### DELETE `/products/{id}`
| **Frontend page** | Products list |
| **Permission** | `Product.Delete` |
| **Response** | 204 (soft delete) |

---

## 4. Customers

| Permission prefix | `Customer.*` |

### POST `/customers`
| | |
|---|---|
| **Purpose** | Create customer |
| **Frontend page** | `/customers/new` |
| **Permission** | `Customer.Create` |
| **Request** | `CreateCustomerRequest` |
| **Response** | `{ id }` (201) |

**Request fields:**
| Field | Validation |
|-------|------------|
| `firstName` | Required, max 100 |
| `lastName` | Required, max 100 |
| `email` | Required, valid, max 256 |
| `phoneNumber` | Required, max 50 |
| `address` | Required, max 500 |
| `city` | Max 100 |
| `country` | Required, max 100 |
| `postalCode` | Max 20 |

**Business rules:** Email unique per tenant (409).

---

### GET `/customers`
| | |
|---|---|
| **Purpose** | Customer directory |
| **Frontend page** | `/customers` |
| **Permission** | `Customer.View` |
| **Query** | `search`, `city?`, `country?`, `page`, `pageSize`, `sortBy`, `sortOrder` |
| **Response** | `PagedResult<CustomerSummaryResponse>` |

**CustomerSummaryResponse:** `id`, `fullName`, `email`, `phoneNumber`, `city`, `country`, `isActive`, `createdAt`

---

### GET `/customers/{id}`
| **Frontend page** | `/customers/:id` |
| **Permission** | `Customer.View` |
| **Response** | `CustomerResponse` (full detail + timestamps) |

---

### PUT `/customers/{id}`
| **Frontend page** | `/customers/:id/edit` |
| **Permission** | `Customer.Update` |
| **Request** | `UpdateCustomerRequest` (create fields + `isActive`) |

---

### DELETE `/customers/{id}`
| **Permission** | `Customer.Delete` |
| **Response** | 204 (soft delete) |

---

### GET `/customers/{id}/orders`
| | |
|---|---|
| **Purpose** | Customer purchase history |
| **Frontend page** | `/customers/:id` → Orders tab |
| **Permission** | `Customer.View` |
| **Response** | `PagedResult<CustomerOrderResponse>` |

**CustomerOrderResponse:** `id`, `orderNumber`, `orderDate`, `status`, `grandTotal`, `createdAt`

---

### GET `/customers/{id}/analytics`
| | |
|---|---|
| **Purpose** | Customer KPIs |
| **Frontend page** | `/customers/:id` → Overview tab |
| **Permission** | `Customer.View` |
| **Response** | `CustomerAnalyticsResponse` |

**CustomerAnalyticsResponse:** `totalOrders`, `totalSpending`, `averageOrderValue`, `lastOrderDate`, `totalCompletedOrders`

**UI note:** Use `totalSpending` as proxy for "lifetime value"; no outstanding balance in backend.

---

## 5. Orders

| Permission prefix | `Order.*` |

### POST `/orders`
| | |
|---|---|
| **Purpose** | Create sales order; deducts stock |
| **Frontend page** | `/orders/new` |
| **Permission** | `Order.Create` |
| **Request** | `{ customerId, discount, tax, items: [{ productId, quantity }] }` |
| **Response** | `{ id }` (201) |

**Validation:**
- `customerId` required
- `discount`, `tax` ≥ 0
- ≥ 1 item; `quantity` > 0 and ≤ 10,000

**Business rules:**
- Customer must be active
- Products must exist and be active
- Unit price from product `salePrice`
- Stock checked and deducted
- `grandTotal` must be ≥ 0

---

### GET `/orders`
| | |
|---|---|
| **Purpose** | Order list with filters |
| **Frontend page** | `/orders` |
| **Permission** | `Order.View` |
| **Query** | `status?` (Pending/Confirmed/Processing/Completed/Cancelled), `search`, pagination, sort |
| **Response** | `PagedResult<OrderSummaryDto>` |

---

### GET `/orders/{id}`
| **Frontend page** | `/orders/:id` |
| **Permission** | `Order.View` |
| **Response** | `OrderDto` (full detail + customer snapshot + items) |

---

### PUT `/orders/{id}`
| **Frontend page** | `/orders/:id/edit` |
| **Permission** | `Order.Update` |
| **Request** | `{ discount, tax, items[] }` |
| **Business rules** | Only Pending or Confirmed orders editable |

---

### DELETE `/orders/{id}`
| **Permission** | `Order.Delete` |
| **Business rules** | Not allowed for Processing/Completed |

---

### PATCH `/orders/{id}/status`
| | |
|---|---|
| **Purpose** | Advance order through workflow |
| **Frontend page** | Order detail status dropdown |
| **Permission** | `Order.Update` |
| **Request** | `{ status }` |

**Status transitions:**
```
Pending → Confirmed | Cancelled
Confirmed → Processing | Cancelled
Processing → Completed | Cancelled
Completed → (terminal)
Cancelled → (terminal)
```

**Business rules:** Cancel restores stock (unless already Completed/Cancelled).

---

## 6. Inventory

| Permission prefix | `Inventory.View`, `Inventory.Update`, `Inventory.Adjust` |

### GET `/inventory`
| | |
|---|---|
| **Purpose** | Stock levels list |
| **Frontend page** | `/inventory` |
| **Permission** | `Inventory.View` |
| **Query** | `search`, `lowStock?`, `outOfStock?`, pagination, sort |
| **Response** | `PagedResult<InventorySummaryResponse>` |

---

### GET `/inventory/{productId}`
| **Frontend page** | `/inventory/:productId` |
| **Permission** | `Inventory.View` |
| **Response** | `InventoryResponse` |

---

### PUT `/inventory/{productId}`
| **Purpose** | Update stock thresholds |
| **Frontend page** | `/inventory/:productId/settings` |
| **Permission** | `Inventory.Update` |
| **Request** | `{ minimumStockLevel, maximumStockLevel, reorderLevel }` |
| **Validation** | min ≥ 0, reorder ≥ 0, max ≥ min |

---

### POST `/inventory/increase`
| **Purpose** | Receive stock |
| **Frontend page** | `/inventory/receive` |
| **Permission** | `Inventory.Adjust` |
| **Request** | `{ productId, quantity, referenceNumber?, notes? }` |
| **Validation** | quantity 0–1,000,000 |

---

### POST `/inventory/decrease`
| **Purpose** | Manual stock reduction |
| **Frontend page** | `/inventory/adjust` |
| **Permission** | `Inventory.Adjust` |

---

### POST `/inventory/adjust`
| **Purpose** | Typed stock movement |
| **Frontend page** | `/inventory/adjust` |
| **Permission** | `Inventory.Adjust` |
| **Request** | `{ productId, quantity, transactionType, referenceNumber?, notes? }` |
| **transactionType** | Purchase, Sale, Adjustment, Return, Damage, Transfer |

---

### GET `/inventory/transactions`
| **Frontend page** | `/inventory/transactions` |
| **Permission** | `Inventory.View` |
| **Query** | `productId?`, `transactionType?`, `search`, pagination |
| **Response** | `PagedResult<StockTransactionResponse>` |

---

### GET `/inventory/analytics`
| **Frontend page** | `/inventory` dashboard cards |
| **Permission** | `Inventory.View` |
| **Response** | `InventoryAnalyticsResponse` |

---

### GET `/inventory/low-stock`
| **Frontend page** | Dashboard widget + `/inventory/alerts` |
| **Permission** | `Inventory.View` |
| **Response** | `InventorySummaryResponse[]` |

---

### GET `/inventory/out-of-stock`
| **Frontend page** | `/inventory/alerts` |
| **Permission** | `Inventory.View` |

---

### GET `/inventory/reorder-products`
| **Frontend page** | `/inventory/reorder` |
| **Permission** | `Inventory.View` |

---

## 7. Dashboard & Reports

Dashboard endpoints serve as the **Reports module backend** until dedicated report APIs exist.

### Common query parameters
| Param | Values |
|-------|--------|
| `period` | `today`, `week`, `month`, `year`, `all`, `custom` |
| `startDate`, `endDate` | Required when `period=custom` |
| `top` | 10 or 20 (product charts only) |

### GET `/dashboard/overview`
| **Frontend page** | `/dashboard` |
| **Permission** | `Order.View` |
| **Response** | `DashboardOverviewResponse` |

---

### GET `/dashboard/sales`
| **Frontend page** | `/dashboard`, `/reports/sales` |
| **Permission** | `Order.View` |
| **Response** | `SalesAnalyticsResponse` (`todaySales`, `monthlySales`, trends, etc.) |

---

### GET `/dashboard/customers`
| **Frontend page** | `/reports/customers` |
| **Permission** | `Customer.View` |
| **Response** | `CustomerAnalyticsDashboardResponse` |

---

### GET `/dashboard/products`
| **Frontend page** | `/reports/products` |
| **Permission** | `Product.View` |
| **Response** | `ProductAnalyticsResponse` |

---

### GET `/dashboard/inventory`
| **Frontend page** | `/reports/inventory` |
| **Permission** | `Inventory.View` |
| **Response** | `InventoryAnalyticsDashboardResponse` |

---

### GET `/dashboard/orders`
| **Frontend page** | `/dashboard`, `/reports/orders` |
| **Permission** | `Order.View` |
| **Response** | `OrderAnalyticsResponse` |

---

### Chart endpoints
| Route | Frontend widget | Permission |
|-------|-----------------|------------|
| `/dashboard/charts/revenue` | Revenue line chart | `Order.View` |
| `/dashboard/charts/orders` | Orders bar chart | `Order.View` |
| `/dashboard/charts/customers` | Customer growth | `Customer.View` |
| `/dashboard/charts/products` | Product performance | `Product.View` |
| `/dashboard/charts/inventory` | Stock movement | `Inventory.View` |

**Response:** `ChartDataResponse` — `chartType`, `title`, `labels[]`, `datasets[]`, `dateRange`

---

## 8. Users & RBAC

### Roles — `/api/roles`

| Method | Route | Purpose | Permission | Frontend page |
|--------|-------|---------|------------|---------------|
| POST | `/roles` | Create role | `Role.Create` | `/roles/new` |
| GET | `/roles` | List roles | `Role.View` | `/roles` |
| GET | `/roles/{id}` | Role detail | `Role.View` | `/roles/:id` |
| PUT | `/roles/{id}` | Update role | `Role.Update` | `/roles/:id/edit` |
| DELETE | `/roles/{id}` | Delete role | `Role.Delete` | `/roles` |
| POST | `/roles/{roleId}/permissions` | Grant permission | `Role.Update` | Role permission matrix |
| DELETE | `/roles/{roleId}/permissions/{permissionId}` | Revoke | `Role.Update` | Role permission matrix |

**RoleDto:** `id`, `name`, `description`, `isActive`, `createdAt`, `permissions[]`

**Business rules:** System roles (Admin, Manager, Sales, InventoryManager, Viewer) cannot be deleted.

---

### Permissions — `/api/permissions`

| Method | Route | Permission | Frontend page |
|--------|-------|------------|---------------|
| GET | `/permissions` | `Role.View` | `/permissions` |
| GET | `/permissions/{id}` | `Role.View` | `/permissions/:id` |

**PermissionDto:** `id`, `name`, `code`, `description`, `category`

---

### Users — `/api/users` (partial)

| Method | Route | Purpose | Permission | Frontend page |
|--------|-------|---------|------------|---------------|
| POST | `/users/{userId}/roles` | Assign role | `User.Update` | `/users/:id/roles` |
| DELETE | `/users/{userId}/roles/{roleId}` | Remove role | `User.Update` | `/users/:id/roles` |

### ⚠️ Missing user endpoints (permissions defined, no API)

| Planned route | Permission | Frontend page |
|---------------|------------|---------------|
| GET `/users` | `User.View` | `/users` |
| POST `/users` | `User.Create` | `/users/invite` |
| GET `/users/{id}` | `User.View` | `/users/:id` |
| PUT `/users/{id}` | `User.Update` | `/users/:id/edit` |
| DELETE `/users/{id}` | `User.Delete` | `/users` |

---

## 9. Settings (Not Implemented)

| Planned endpoint | Purpose | Frontend page |
|------------------|---------|---------------|
| GET `/tenants/current` | Load business profile | `/settings/business` |
| PUT `/tenants/current` | Update business info | `/settings/business` |
| PUT `/tenants/current/logo` | Upload logo | `/settings/branding` |
| GET `/settings/preferences` | Currency, timezone, tax | `/settings/preferences` |
| PUT `/settings/preferences` | Save preferences | `/settings/preferences` |

**Tenant entity fields available for future API:** `name`, `businessType`, `email`, `phone`, `address`, `subscriptionPlan`, `isActive`

---

## 10. Invoices (Not Implemented)

| Planned endpoint | Purpose | Frontend page |
|------------------|---------|---------------|
| GET `/invoices` | List invoices | `/invoices` |
| POST `/invoices` | Create from order | `/invoices/new` |
| GET `/invoices/{id}` | Invoice detail | `/invoices/:id` |
| PUT `/invoices/{id}` | Edit draft | `/invoices/:id/edit` |
| DELETE `/invoices/{id}` | Void invoice | `/invoices` |
| POST `/invoices/{id}/payments` | Record payment | `/invoices/:id/pay` |
| GET `/invoices/{id}/pdf` | Download PDF | Invoice detail |
| POST `/invoices/{id}/email` | Email to customer | Invoice detail |

**Interim:** Use `GET /orders/{id}` + print template as "Order Invoice" until Payment API ships.

**Payment entity fields:** `orderId`, `customerId`, `amount`, `paymentMethod`, `paymentDate`, `referenceNo`

---

## 11. Endpoint Count Summary

| Domain | Endpoints | Status |
|--------|-----------|--------|
| Auth | 2 | ✅ |
| Categories | 5 | ✅ |
| Products | 6 | ✅ |
| Customers | 7 | ✅ |
| Orders | 6 | ✅ |
| Inventory | 11 | ✅ |
| Dashboard | 11 | ✅ |
| Roles/Permissions/Users | 10 | ⚠️ Users partial |
| Settings | 0 | ❌ |
| Invoices | 0 | ❌ |
| **Total implemented** | **58** | |
