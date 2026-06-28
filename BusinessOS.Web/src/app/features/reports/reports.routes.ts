import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const REPORT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./reports/reports.component').then((m) => m.ReportsComponent),
    title: 'Reports | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.order.view])],
  },
];
