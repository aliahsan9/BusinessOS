import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const ANALYTICS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./analytics.component').then((m) => m.AnalyticsComponent),
    canActivate: [permissionGuard([PermissionCodes.report.view])],
    title: 'Business Analytics | BusinessOS',
  },
];
