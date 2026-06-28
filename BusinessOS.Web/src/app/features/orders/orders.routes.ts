import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const ORDER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./order-list/order-list.component').then((m) => m.OrderListComponent),
    title: 'Orders | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.order.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./order-form/order-form.component').then((m) => m.OrderFormComponent),
    title: 'Create Order | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.order.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./order-form/order-form.component').then((m) => m.OrderFormComponent),
    title: 'Edit Order | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.order.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./order-detail/order-detail.component').then((m) => m.OrderDetailComponent),
    title: 'Order Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.order.view])],
  },
];
