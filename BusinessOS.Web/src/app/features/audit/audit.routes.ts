import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./audit-log-list/audit-log-list.component').then((m) => m.AuditLogListComponent),
    title: 'Audit Logs | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.audit.view])],
  },
];
