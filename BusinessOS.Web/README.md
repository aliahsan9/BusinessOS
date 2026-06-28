# BusinessOS Web Frontend

Production-ready Angular 19 frontend for the BusinessOS backend API.

## Tech Stack

- **Angular 19.2** with standalone components
- **TypeScript** · **Bootstrap 5** · **SCSS**
- **Angular Signals** for reactive state
- **RxJS** · **Angular Router** · **Reactive Forms**
- **HttpClient** with interceptors and guards
- **Chart.js** for dashboard analytics
- **Jasmine/Karma** unit tests · **Playwright** E2E tests

## Prerequisites

- Node.js 20+
- BusinessOS API running at `http://localhost:5162`

## Quick Start

```bash
cd BusinessOS.Web
npm install
npm start
```

Open [http://localhost:4200](http://localhost:4200)

The dev server proxies `/api` requests to the backend via `proxy.conf.json`.

## Project Structure

```
src/app/
├── core/           # Services, guards, interceptors, models, constants
├── shared/         # Reusable components, layouts, validators
├── features/       # Feature modules (auth, dashboard, CRUD modules)
├── state/          # Signal-based state services
└── app-routing.ts  # Root routing configuration
```

## Authentication

| Route | Description |
|-------|-------------|
| `/auth/login` | Sign in with email/password |
| `/auth/register` | Create account + tenant |
| `/auth/forgot-password` | Request reset (UI ready; backend pending) |
| `/auth/reset-password` | Reset with token (UI ready; backend pending) |

JWT is stored in `localStorage`. The auth interceptor attaches:
- `Authorization: Bearer <token>`
- `X-Tenant-ID: <tenantId>`

**Note:** The backend has no refresh-token endpoint. Sessions expire after 60 minutes; users must re-login.

## Dashboard

The dashboard at `/dashboard` loads data from:

- `/api/dashboard/overview`
- `/api/dashboard/sales`
- `/api/dashboard/customers`
- `/api/dashboard/products`
- `/api/dashboard/inventory`
- `/api/dashboard/orders`
- `/api/dashboard/charts/revenue`
- `/api/dashboard/charts/orders`

Requires `Order.View` permission.

## Environment Configuration

| File | API URL |
|------|---------|
| `src/environments/environment.ts` | `http://localhost:5162/api` |
| `src/environments/environment.prod.ts` | `/api` (same-origin) |

## Scripts

| Command | Description |
|---------|-------------|
| `npm start` | Dev server with API proxy |
| `npm run build:prod` | Production build |
| `npm test` | Unit tests (watch mode) |
| `npm run test:ci` | Headless tests with coverage |
| `npm run e2e` | Playwright E2E tests |

## Design System

- **Font:** Inter (300–700)
- **Primary:** `#2563EB`
- CSS variables in `src/styles/_variables.scss`
- Dark mode variables prepared via `[data-theme='dark']`

## Completed Modules (Phase 1)

- [x] Project setup
- [x] Core module (API layer, auth, interceptors, guards)
- [x] Shared components & layouts
- [x] Authentication (login, register, forgot/reset password)
- [x] Dashboard with charts and KPIs

## Upcoming Modules (Phase 2)

- Users, Roles, Permissions
- Customers, Products, Inventory
- Orders, Invoices, Reports, Settings

## Backend API Reference

Dev API: `http://localhost:5162/api`  
OpenAPI docs: `http://localhost:5162/scalar` (Development only)

## License

See repository root LICENSE.txt.
