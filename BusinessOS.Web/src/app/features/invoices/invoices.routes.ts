import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const INVOICE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./invoice-list/invoice-list.component').then((m) => m.InvoiceListComponent),
    title: 'Invoices | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.invoice.view])],
  },
  {
    path: ':id',
    loadComponent: () => import('./invoice-detail/invoice-detail.component').then((m) => m.InvoiceDetailComponent),
    title: 'Invoice Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.invoice.view])],
  },
];
