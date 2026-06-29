import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const BILLING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./billing-dashboard/billing-dashboard.component').then((m) => m.BillingDashboardComponent),
    title: 'Billing | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.subscription.view])],
  },
  {
    path: 'usage',
    loadComponent: () => import('./usage-page/usage-page.component').then((m) => m.UsagePageComponent),
    title: 'Usage | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.subscription.view])],
  },
  {
    path: 'history',
    loadComponent: () =>
      import('./transaction-history/transaction-history.component').then((m) => m.TransactionHistoryComponent),
    title: 'Transaction History | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.subscription.view])],
  },
  {
    path: 'invoices',
    loadComponent: () =>
      import('./invoice-history/invoice-history.component').then((m) => m.InvoiceHistoryComponent),
    title: 'Invoices | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.subscription.view])],
  },
];

export const PRICING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pricing-page/pricing-page.component').then((m) => m.PricingPageComponent),
    title: 'Pricing | BusinessOS',
  },
];
