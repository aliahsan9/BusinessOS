import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const SUPPLIER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./supplier-list/supplier-list.component').then((m) => m.SupplierListComponent),
    title: 'Suppliers | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.supplier.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./supplier-form/supplier-form.component').then((m) => m.SupplierFormComponent),
    title: 'Create Supplier | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.supplier.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./supplier-form/supplier-form.component').then((m) => m.SupplierFormComponent),
    title: 'Edit Supplier | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.supplier.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./supplier-detail/supplier-detail.component').then((m) => m.SupplierDetailComponent),
    title: 'Supplier Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.supplier.view])],
  },
];
