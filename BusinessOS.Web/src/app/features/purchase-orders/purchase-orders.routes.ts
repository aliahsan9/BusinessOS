import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const PURCHASE_ORDER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./purchase-order-list/purchase-order-list.component').then((m) => m.PurchaseOrderListComponent),
    title: 'Purchase Orders | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.purchaseOrder.view])],
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./purchase-order-form/purchase-order-form.component').then((m) => m.PurchaseOrderFormComponent),
    title: 'Create Purchase Order | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.purchaseOrder.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./purchase-order-form/purchase-order-form.component').then((m) => m.PurchaseOrderFormComponent),
    title: 'Edit Purchase Order | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.purchaseOrder.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./purchase-order-detail/purchase-order-detail.component').then((m) => m.PurchaseOrderDetailComponent),
    title: 'Purchase Order Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.purchaseOrder.view])],
  },
];
