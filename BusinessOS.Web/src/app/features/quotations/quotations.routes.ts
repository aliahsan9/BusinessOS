import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const QUOTATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./quotation-list/quotation-list.component').then((m) => m.QuotationListComponent),
    title: 'Quotations | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.quotation.view])],
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./quotation-form/quotation-form.component').then((m) => m.QuotationFormComponent),
    title: 'Create Quotation | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.quotation.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./quotation-form/quotation-form.component').then((m) => m.QuotationFormComponent),
    title: 'Edit Quotation | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.quotation.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./quotation-detail/quotation-detail.component').then((m) => m.QuotationDetailComponent),
    title: 'Quotation Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.quotation.view])],
  },
];
