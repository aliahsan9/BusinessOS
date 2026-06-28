import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const PAYMENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./payment-list/payment-list.component').then((m) => m.PaymentListComponent),
    title: 'Payments | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.payment.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./payment-form/payment-form.component').then((m) => m.PaymentFormComponent),
    title: 'Record Payment | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.payment.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./payment-form/payment-form.component').then((m) => m.PaymentFormComponent),
    title: 'Edit Payment | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.payment.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./payment-detail/payment-detail.component').then((m) => m.PaymentDetailComponent),
    title: 'Payment Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.payment.view])],
  },
];
