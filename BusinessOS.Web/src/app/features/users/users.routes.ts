import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const USER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./user-list/user-list.component').then((m) => m.UserListComponent),
    title: 'Users | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.user.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./user-form/user-form.component').then((m) => m.UserFormComponent),
    title: 'Create User | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.user.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./user-form/user-form.component').then((m) => m.UserFormComponent),
    title: 'Edit User | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.user.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./user-detail/user-detail.component').then((m) => m.UserDetailComponent),
    title: 'User Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.user.view])],
  },
];
