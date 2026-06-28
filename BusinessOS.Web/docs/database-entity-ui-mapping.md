# Database Entity → UI Mapping

> Maps backend domain entities and DTO fields to frontend form labels, table columns, and display formatting.

---

## 1. Mapping Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Field exists in API DTO — bind directly |
| 📝 | UI-only field (computed or local until backend) |
| ❌ | Entity field with no API exposure yet |
| 🔒 | Read-only in UI |

---

## 2. Tenant (Business)

**Entity:** `Tenant` · **API:** Created on register only — no update endpoint

| Entity field | UI label | Form control | Page | Status |
|--------------|----------|--------------|------|--------|
| `name` | Business name | Text input | Register, Settings, Onboarding | ✅ via `businessName` on register |
| `businessType` | Business type | Select (Retail, Wholesale, Distributor, Service, Other) | Onboarding, Settings | ❌ |
| — | Industry | Select | Onboarding | 📝 UI-only |
| `email` | Business email | Email input | Settings | ❌ |
| `phone` | Phone number | Tel input | Settings | ❌ |
| `address` | Street address | Textarea | Settings | ❌ |
| — | City | Text input | Settings | 📝 |
| — | Country | Country select | Settings | 📝 |
| — | Tax number | Text input | Settings, Onboarding | 📝 |
| — | Logo | File upload | Branding | 📝 preview only |
| `subscriptionPlan` | Plan | Badge (read-only) | Settings | 🔒 ❌ |
| `isActive` | Status | Badge | Admin only | 🔒 ❌ |
| `ownerUserId` | Owner | Hidden | — | 🔒 |

**Settings UI-only preferences (no entity yet):**

| UI field | Control | Default |
|----------|---------|---------|
| Currency | Select (USD, PKR, EUR, GBP, AED, SAR…) | USD |
| Timezone | Select (IANA) | Browser timezone |
| Language | Select | en |
| Tax percentage | Number (0–100) | 0 |

---

## 3. ApplicationUser

**Entity:** Identity user · **API:** Auth only

| Entity field | UI label | Page | Status |
|--------------|----------|------|--------|
| `firstName` | First name | Register, Profile | ✅ register |
| `lastName` | Last name | Register, Profile | ✅ register |
| `email` | Email | Register, Login, Profile | ✅ |
| `tenantId` | — | Hidden (from JWT) | 🔒 |
| `isActive` | Active | Users admin | ❌ |

---

## 4. Category

**DTO:** `CategoryDto` · **API:** Full CRUD

| Field | UI label | List column | Form | Validation display |
|-------|----------|-------------|------|-------------------|
| `id` | — | — | Hidden | — |
| `name` | Category name | ✅ Yes | Required text | "Name is required" |
| `description` | Description | Optional | Textarea | Max 1000 chars |

**List actions:** Edit, Delete (with product-count warning from 409 error).

---

## 5. Product

**DTO:** `ProductDto` · **API:** Full CRUD

| Field | UI label | List column | Form | Format |
|-------|----------|-------------|------|--------|
| `id` | — | — | Hidden | — |
| `categoryId` | Category | Category name (join) | Dropdown | Required |
| `name` | Product name | ✅ | Text | Required |
| `sku` | SKU / Item code | ✅ | Text | Required; tooltip: "Unique product code" |
| `description` | Description | — | Textarea | Optional |
| `costPrice` | Cost price | ✅ | Currency input | > 0 |
| `salePrice` | Selling price | ✅ | Currency input | > 0 |
| — | Profit margin | ✅ computed | Read-only | `salePrice - costPrice` |
| — | Margin % | Optional column | Read-only | `(margin/salePrice)*100` |
| `currentStock` | In stock | ✅ | Read-only on form | Number; link to inventory |
| `reorderLevel` | Reorder level | ✅ | Number | ≥ 0; tooltip: "Alert when stock falls below" |
| `isActive` | Active | Badge | Toggle | Active/Inactive |
| — | Product image | Thumbnail | File upload | 📝 disabled |

**Low stock indicator:** `currentStock <= reorderLevel` → amber badge "Low stock".

---

## 6. Customer

**DTOs:** `CustomerSummaryResponse`, `CustomerResponse`, `CustomerAnalyticsResponse`

### List / form fields

| Field | UI label | List | Form | Validation |
|-------|----------|------|------|------------|
| `id` | — | — | Hidden | — |
| `firstName` | First name | — | Text | Required |
| `lastName` | Last name | — | Text | Required |
| `fullName` | Customer | ✅ | Read-only computed | — |
| `email` | Email | ✅ | Email | Required, unique |
| `phoneNumber` | Phone | ✅ | Tel | Required |
| `address` | Address | — | Textarea | Required |
| `city` | City | ✅ filter | Text | Optional |
| `country` | Country | ✅ filter | Select | Required |
| `postalCode` | Postal / ZIP code | — | Text | Optional |
| `isActive` | Status | Badge | Toggle | Edit only |
| `createdAt` | Customer since | ✅ | 🔒 | Date format |
| `updatedAt` | Last updated | Detail | 🔒 | Date format |

### Analytics panel (detail page)

| DTO field | UI label | Format |
|-----------|----------|--------|
| `totalOrders` | Total orders | Integer |
| `totalSpending` | Total spent | Currency |
| `averageOrderValue` | Average order | Currency |
| `lastOrderDate` | Last order | Date or "Never" |
| `totalCompletedOrders` | Completed orders | Integer |

### UI-only fields

| Field | Source | Label |
|-------|--------|-------|
| Notes | localStorage / future API | Customer notes |
| Outstanding balance | ❌ not in backend | Hide or label "Not tracked yet" |

---

## 7. Order

**DTOs:** `OrderSummaryDto`, `OrderDto`, `OrderItemDto`

### Order header

| Field | UI label | List | Detail | Form |
|-------|----------|------|--------|------|
| `id` | — | — | Hidden | — |
| `orderNumber` | Order # | ✅ | ✅ | 🔒 auto |
| `orderDate` | Order date | ✅ | ✅ | 🔒 default today |
| `status` | Status | Badge | Status dropdown | PATCH only |
| `customerId` | Customer | — | — | Search select |
| `customerName` | Customer | ✅ | ✅ | 🔒 |
| `customerEmail` | Email | — | ✅ | 🔒 |
| `customerPhone` | Phone | — | ✅ | 🔒 |
| `customerAddress` | Address | — | Print block | 🔒 |
| `customerCity` | City | — | Print | 🔒 |
| `customerCountry` | Country | — | Print | 🔒 |
| `totalAmount` | Subtotal | — | ✅ | Computed |
| `discount` | Discount | — | ✅ | Currency ≥ 0 |
| `tax` | Tax | — | ✅ | Currency ≥ 0 |
| `grandTotal` | Total | ✅ | ✅ bold | Computed |
| `createdAt` | Created | ✅ | ✅ | Datetime |
| `updatedAt` | Updated | — | ✅ | Datetime |

### Order line items

| Field | UI label | Form column |
|-------|----------|-------------|
| `productId` | Product | Product search select |
| `productName` | Product | Display after select |
| `productSku` | SKU | Display |
| `quantity` | Qty | Number input |
| `unitPrice` | Unit price | 🔒 from product |
| `total` | Line total | 🔒 computed |

### Status badge colors

| Status | Color |
|--------|-------|
| Pending | Warning (amber) |
| Confirmed | Info (blue) |
| Processing | Primary |
| Completed | Success (green) |
| Cancelled | Secondary (gray) |

---

## 8. Inventory

**DTOs:** `InventorySummaryResponse`, `InventoryResponse`, `StockTransactionResponse`

### Stock record

| Field | UI label | List | Detail |
|-------|----------|------|--------|
| `productId` | — | Link | Link to product |
| `productName` | Product | ✅ | ✅ |
| `productSku` | SKU | ✅ | ✅ |
| `currentStock` | Current stock | ✅ | ✅ large number |
| `minimumStockLevel` | Minimum level | ✅ | Form |
| `maximumStockLevel` | Maximum level | — | Form |
| `reorderLevel` | Reorder level | ✅ | Form |
| `suggestedReorderQuantity` | Suggested order qty | Alerts | ✅ |
| `isLowStock` | — | Badge trigger | Alert banner |
| `isOutOfStock` | — | Badge trigger | Alert banner |
| `lastUpdated` | Last updated | — | ✅ |

### Stock transaction

| Field | UI label | History table |
|-------|----------|---------------|
| `transactionType` | Type | Badge (Purchase, Sale, etc.) |
| `quantity` | Quantity | +/- with color |
| `previousStock` | Before | Number |
| `newStock` | After | Number |
| `referenceNumber` | Reference | Text |
| `notes` | Notes | Text |
| `createdAt` | Date | Datetime |

### Adjust form

| Field | UI label | Control |
|-------|----------|---------|
| `productId` | Product | Search select |
| `quantity` | Quantity | Number |
| `transactionType` | Reason | Select enum |
| `referenceNumber` | Reference # | Optional text |
| `notes` | Notes | Optional textarea |

**Transaction type labels (user-friendly):**

| Enum | UI label |
|------|----------|
| Purchase | Received from supplier |
| Sale | Sold to customer |
| Adjustment | Manual correction |
| Return | Customer return |
| Damage | Damaged / lost |
| Transfer | Transfer between locations |

---

## 9. Payment / Invoice (Future)

**Entity:** `Payment` · **API:** Not implemented

| Entity field | UI label | Invoice page |
|--------------|----------|--------------|
| `orderId` | Order | Link |
| `customerId` | Bill to | Customer block |
| `amount` | Amount paid | Currency |
| `paymentMethod` | Payment method | Select (Cash, Card, Bank, Other) |
| `paymentDate` | Payment date | Date picker |
| `referenceNo` | Reference | Text |

**Invoice UI fields (planned):**

| Field | Source |
|-------|--------|
| Invoice number | Generated |
| Due date | Settings + order date |
| Payment status | Paid / Partial / Unpaid |
| Outstanding amount | total - payments |

**Interim:** Map `OrderDto` to print invoice template.

---

## 10. Role & Permission

**DTOs:** `RoleDto`, `PermissionDto`

### Role

| Field | UI label |
|-------|----------|
| `name` | Role name |
| `description` | Description |
| `isActive` | Active |
| `permissions` | Permission checklist |
| `createdAt` | Created |

### Permission catalog

| Field | UI label | Table |
|-------|----------|-------|
| `name` | Permission | ✅ |
| `code` | Code | ✅ monospace |
| `description` | Description | ✅ |
| `category` | Category | ✅ group header |

---

## 11. Dashboard KPI Mapping

| Business question | DTO path | Widget type |
|-------------------|----------|-------------|
| Sales today | `SalesAnalyticsResponse.todaySales` | Stat card |
| Sales this month | `SalesAnalyticsResponse.monthlySales` | Stat card |
| Customer count | `DashboardOverviewResponse.totalCustomers` | Stat card |
| Product count | `DashboardOverviewResponse.totalProducts` | Stat card |
| Low stock count | `DashboardOverviewResponse.lowStockProducts` | Alert card |
| Out of stock | `DashboardOverviewResponse.outOfStockProducts` | Alert card |
| Revenue trend | `ChartDataResponse` revenue | Line chart |
| Orders by status | `OrderAnalyticsResponse.ordersByStatus` | Donut chart |
| Top customers | `CustomerAnalyticsDashboardResponse.topCustomers` | Table |
| Best sellers | `ProductAnalyticsResponse.bestSellingProducts` | Table |

---

## 12. Formatting Conventions

| Data type | Display | Input |
|-----------|---------|-------|
| Currency | `{symbol}{amount}` 2 decimals | `type="number" step="0.01"` |
| Date | `MMM d, yyyy` | Date picker |
| Datetime | `MMM d, yyyy h:mm a` | — |
| Phone | As entered | `type="tel"` |
| GUID | Never show raw | Use names/numbers |
| Boolean | Badge Active/Inactive | Toggle switch |
| Enum status | Colored badge | Dropdown |

**Currency symbol:** Read from Settings preferences (default `$`) until tenant API provides currency code.

---

## 13. Entities Without UI (Future Modules)

| Entity | Planned module |
|--------|----------------|
| `Supplier` | Procurement |
| `Purchase`, `PurchaseItem` | Purchases |
| `Expense` | Expenses |
| `Employee` | HR / Payroll |
| `Notification` | Notification center |
| `AIConversation` | AI assistant |
| `RbacAuditLog` | Admin audit log |

---

## 14. Form → API Request Mapping

| UI form | API request type |
|---------|------------------|
| Product create | `CreateProductCommand` body |
| Customer create | `CreateCustomerRequest` |
| Order create | `{ customerId, discount, tax, items: CreateOrderItemDto[] }` |
| Stock receive | `StockChangeRequest` → `/inventory/increase` |
| Stock adjust | `StockAdjustmentRequest` → `/inventory/adjust` |
| Role create | `CreateRoleRequest` |
| Register | `RegisterCommand` |

TypeScript interfaces in `src/app/models/` should mirror these exactly (camelCase JSON).
