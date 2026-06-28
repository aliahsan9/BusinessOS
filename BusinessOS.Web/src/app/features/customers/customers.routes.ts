import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const CUSTOMER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./customer-list/customer-list.component').then((m) => m.CustomerListComponent),
    title: 'Customers | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.customer.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./customer-form/customer-form.component').then((m) => m.CustomerFormComponent),
    title: 'Create Customer | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.customer.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./customer-form/customer-form.component').then((m) => m.CustomerFormComponent),
    title: 'Edit Customer | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.customer.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./customer-detail/customer-detail.component').then((m) => m.CustomerDetailComponent),
    title: 'Customer Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.customer.view])],
  },
];
