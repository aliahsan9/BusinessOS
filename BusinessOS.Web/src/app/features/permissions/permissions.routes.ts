import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const PERMISSION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./permission-matrix/permission-matrix.component').then((m) => m.PermissionMatrixComponent),
    title: 'Permissions | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.role.view])],
  },
];
