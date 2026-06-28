import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const INVENTORY_ROUTES: Routes = [
  { path: '', redirectTo: 'overview', pathMatch: 'full' },
  {
    path: 'overview',
    loadComponent: () =>
      import('./inventory-overview/inventory-overview.component').then((m) => m.InventoryOverviewComponent),
    title: 'Inventory Overview | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.inventory.view])],
  },
  {
    path: 'stock-levels',
    loadComponent: () => import('./stock-levels/stock-levels.component').then((m) => m.StockLevelsComponent),
    title: 'Stock Levels | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.inventory.view])],
  },
  {
    path: 'history',
    loadComponent: () => import('./stock-history/stock-history.component').then((m) => m.StockHistoryComponent),
    title: 'Stock History | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.inventory.view])],
  },
  {
    path: 'reports',
    loadComponent: () =>
      import('./inventory-reports/inventory-reports.component').then((m) => m.InventoryReportsComponent),
    title: 'Inventory Reports | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.inventory.view])],
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./inventory-dashboard/inventory-dashboard.component').then((m) => m.InventoryDashboardComponent),
    title: 'Inventory Dashboard | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.inventory.view])],
  },
];
