import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const REPORT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./report-hub/report-hub.component').then((m) => m.ReportHubComponent),
    title: 'PDF Reports | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.report.view])],
  },
  {
    path: 'data',
    loadComponent: () => import('./reports/reports.component').then((m) => m.ReportsComponent),
    title: 'Data Reports | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.report.view])],
  },
];
