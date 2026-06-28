import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const ROLE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./role-list/role-list.component').then((m) => m.RoleListComponent),
    title: 'Roles | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.role.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./role-form/role-form.component').then((m) => m.RoleFormComponent),
    title: 'Create Role | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.role.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./role-form/role-form.component').then((m) => m.RoleFormComponent),
    title: 'Edit Role | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.role.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./role-detail/role-detail.component').then((m) => m.RoleDetailComponent),
    title: 'Role Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.role.view])],
  },
];
