import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const TENANT_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full',
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./tenant-dashboard/tenant-dashboard.component').then((m) => m.TenantDashboardComponent),
    title: 'Tenant Dashboard | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.tenant.view])],
  },
  {
    path: 'settings',
    loadComponent: () =>
      import('./tenant-settings/tenant-settings.component').then((m) => m.TenantSettingsComponent),
    title: 'Tenant Settings | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.tenant.view])],
  },
];
