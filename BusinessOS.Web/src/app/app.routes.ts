import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { onboardingRedirectGuard } from './core/guards/onboarding.guard';
import { themeGuard } from './core/theme/theme.guard';
import { buildFeatureRoutes } from './app-feature.routes';
import { ROUTES } from './core/constants/route.constants';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: 'register-business',
    loadComponent: () =>
      import('./features/tenant/business-registration/business-registration.component').then(
        (m) => m.BusinessRegistrationComponent,
      ),
    canActivate: [guestGuard],
    title: 'Register Business | BusinessOS',
  },
  {
    path: 'onboarding',
    loadChildren: () => import('./features/onboarding/onboarding.routes').then((m) => m.ONBOARDING_ROUTES),
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./shared/pages/forbidden-page/forbidden-page.component').then((m) => m.ForbiddenPageComponent),
    title: 'Access Denied | BusinessOS',
  },
  {
    path: 'not-found',
    loadComponent: () =>
      import('./shared/pages/not-found-page/not-found-page.component').then((m) => m.NotFoundPageComponent),
    title: 'Page Not Found | BusinessOS',
  },
  {
    path: '',
    loadComponent: () =>
      import('./shared/layouts/dashboard-layout/dashboard-layout.component').then((m) => m.DashboardLayoutComponent),
    canActivate: [authGuard, themeGuard, onboardingRedirectGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'products',
        loadChildren: () => import('./features/products/products.routes').then((m) => m.PRODUCT_ROUTES),
      },
      {
        path: 'inventory',
        loadChildren: () => import('./features/inventory/inventory.routes').then((m) => m.INVENTORY_ROUTES),
      },
      {
        path: 'suppliers',
        loadChildren: () => import('./features/suppliers/suppliers.routes').then((m) => m.SUPPLIER_ROUTES),
      },
      {
        path: 'purchase-orders',
        loadChildren: () => import('./features/purchase-orders/purchase-orders.routes').then((m) => m.PURCHASE_ORDER_ROUTES),
      },
      {
        path: 'customers',
        loadChildren: () => import('./features/customers/customers.routes').then((m) => m.CUSTOMER_ROUTES),
      },
      {
        path: 'orders',
        loadChildren: () => import('./features/orders/orders.routes').then((m) => m.ORDER_ROUTES),
      },
      {
        path: 'quotations',
        loadChildren: () => import('./features/quotations/quotations.routes').then((m) => m.QUOTATION_ROUTES),
      },
      {
        path: 'invoices',
        loadChildren: () => import('./features/invoices/invoices.routes').then((m) => m.INVOICE_ROUTES),
      },
      {
        path: 'payments',
        loadChildren: () => import('./features/payments/payments.routes').then((m) => m.PAYMENT_ROUTES),
      },
      {
        path: 'sales',
        loadChildren: () => import('./features/sales/sales.routes').then((m) => m.SALES_ROUTES),
      },
      {
        path: 'reports',
        loadChildren: () => import('./features/reports/reports.routes').then((m) => m.REPORT_ROUTES),
      },
      {
        path: 'analytics',
        loadChildren: () => import('./features/analytics/analytics.routes').then((m) => m.ANALYTICS_ROUTES),
      },
      {
        path: 'expenses',
        loadChildren: () => import('./features/expenses/expenses.routes').then((m) => m.EXPENSE_ROUTES),
      },
      {
        path: 'expense-categories',
        loadChildren: () =>
          import('./features/expense-categories/expense-categories.routes').then((m) => m.EXPENSE_CATEGORY_ROUTES),
      },
      {
        path: 'finance',
        loadChildren: () => import('./features/finance/finance.routes').then((m) => m.FINANCE_ROUTES),
      },
      {
        path: 'users',
        loadChildren: () => import('./features/users/users.routes').then((m) => m.USER_ROUTES),
      },
      {
        path: 'roles',
        loadChildren: () => import('./features/roles/roles.routes').then((m) => m.ROLE_ROUTES),
      },
      {
        path: 'permissions',
        loadChildren: () => import('./features/permissions/permissions.routes').then((m) => m.PERMISSION_ROUTES),
      },
      {
        path: 'audit',
        loadChildren: () => import('./features/audit/audit.routes').then((m) => m.AUDIT_ROUTES),
      },
      {
        path: 'notifications',
        loadChildren: () => import('./features/notifications/notifications.routes').then((m) => m.NOTIFICATION_ROUTES),
      },
      {
        path: 'activity',
        loadChildren: () => import('./features/activity/activity.routes').then((m) => m.ACTIVITY_ROUTES),
      },
      {
        path: 'settings',
        loadChildren: () => import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        path: 'help',
        loadChildren: () => import('./features/help/help.routes').then((m) => m.HELP_ROUTES),
      },
      {
        path: 'subscription',
        loadChildren: () => import('./features/subscription/subscription.routes').then((m) => m.SUBSCRIPTION_ROUTES),
      },
      {
        path: 'billing',
        loadChildren: () => import('./features/billing/billing.routes').then((m) => m.BILLING_ROUTES),
      },
      {
        path: 'pricing',
        loadChildren: () => import('./features/billing/billing.routes').then((m) => m.PRICING_ROUTES),
      },
      {
        path: 'admin',
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      {
        path: 'team',
        loadChildren: () => import('./features/team/team.routes').then((m) => m.TEAM_ROUTES),
      },
      {
        path: 'organization',
        loadChildren: () => import('./features/organization/organization.routes').then((m) => m.ORGANIZATION_ROUTES),
      },
      {
        path: 'tenant-settings',
        redirectTo: 'tenant/settings',
        pathMatch: 'full',
      },
      {
        path: 'tenant',
        loadChildren: () => import('./features/tenant/tenant.routes').then((m) => m.TENANT_ROUTES),
      },
      ...buildFeatureRoutes(),
    ],
  },
  { path: '**', redirectTo: ROUTES.notFound.replace(/^\//, '') },
];
