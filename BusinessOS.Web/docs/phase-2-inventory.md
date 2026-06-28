# Phase 2 — Product, Supplier & Inventory Management

Phase 2 delivers the inventory management stack for BusinessOS: products, categories, suppliers, inventory, stock movements, purchase orders, and an inventory dashboard.

## Architecture

### Frontend (Angular 19)

```
features/
├── products/          # Catalog CRUD + categories
├── inventory/         # Stock levels, history, reports, dashboard
├── suppliers/         # Supplier CRUD + purchase/product history
└── purchase-orders/   # PO workflow with line items

core/
├── models/            # DTOs aligned with backend APIs
├── services/          # BaseApiService extensions (retry + error mapping)
└── helpers/           # Export CSV/Excel, profit margin calculations

state/
├── product.state.ts
├── category.state.ts
└── inventory.state.ts
```

**Patterns used:**
- Standalone components with `ChangeDetectionStrategy.OnPush`
- Signal-based state services (no NgRx)
- Lazy-loaded feature routes with `permissionGuard`
- Shared UI: `app-page-header`, `app-search-bar`, `app-confirm-dialog`, existing design system

### Backend (added in Phase 2)

| Module | Route | Status |
|--------|-------|--------|
| Categories | `/api/categories` | Existing |
| Products | `/api/products` | Existing |
| Inventory | `/api/inventory` | Existing |
| Suppliers | `/api/suppliers` | **New** |
| Purchase Orders | `/api/purchase-orders` | **New** |

Run migration before testing backend changes:

```bash
dotnet ef database update --project BusinessOS.Infrastructure --startup-project BusinessOS.API
```

## Routes

| Path | Module |
|------|--------|
| `/products` | Product list |
| `/products/new` | Create product |
| `/products/:id` | Product details |
| `/products/:id/edit` | Edit product |
| `/products/categories` | Category list |
| `/inventory/overview` | Inventory hub |
| `/inventory/stock-levels` | Stock table + adjustments |
| `/inventory/history` | Stock movement audit trail |
| `/inventory/reports` | Value & profit reports |
| `/inventory/dashboard` | KPI widgets & charts |
| `/suppliers` | Supplier management |
| `/purchase-orders` | Purchase order workflow |

## API Field Mapping

Products use backend DTO fields directly. Client-side derived fields:

- **Profit margin** — `(salePrice - costPrice) / salePrice`
- **Stock value** — `currentStock × costPrice`

Not in backend (UI notes only):

- Barcode, product images (reserved for future API)
- Reserved stock (shown as equal to current stock until orders module links)
- Nested categories (backend uses flat categories)

## Bulk Operations

Bulk delete/update on products runs sequential API calls (no dedicated bulk endpoint).

## Export

CSV and Excel export are client-side via `ExportHelper` (Excel uses CSV with spreadsheet MIME type).

## Permissions

| Module | Permissions |
|--------|-------------|
| Category | `Category.*` |
| Product | `Product.*` |
| Inventory | `Inventory.View`, `Inventory.Update`, `Inventory.Adjust` |
| Supplier | `Supplier.*` |
| Purchase Order | `PurchaseOrder.*` |

## Testing

```bash
cd BusinessOS.Web
npm run test:ci    # Unit tests + coverage
npm run build      # Production build
```

Service tests: `category.service.spec.ts`, `product.service.spec.ts`, `inventory.service.spec.ts`  
Helper tests: `export.helper.spec.ts`  
Component specs: each feature component includes a smoke test.

## Purchase Order Workflow

```
Draft → Pending → Approved → Received
         ↓           ↓
      Cancelled   Cancelled
```

Receiving a PO increases stock via the inventory service for each line item.
