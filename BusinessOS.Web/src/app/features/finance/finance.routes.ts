import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const FINANCE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./financial-dashboard/financial-dashboard.component').then((m) => m.FinancialDashboardComponent),
    title: 'Financial Dashboard | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.finance.view])],
  },
  {
    path: 'profit-loss',
    loadComponent: () => import('./profit-loss/profit-loss.component').then((m) => m.ProfitLossComponent),
    title: 'Profit & Loss | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.finance.view])],
  },
];
