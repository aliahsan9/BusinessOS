import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./system-dashboard/system-dashboard.component').then((m) => m.SystemDashboardComponent),
    title: 'System Admin | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.systemAdmin.view])],
  },
];
