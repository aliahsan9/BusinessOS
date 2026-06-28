import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const EXPENSE_CATEGORY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./category-list/category-list.component').then((m) => m.CategoryListComponent),
    title: 'Expense Categories | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expenseCategory.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./category-form/category-form.component').then((m) => m.CategoryFormComponent),
    title: 'Create Expense Category | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expenseCategory.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./category-form/category-form.component').then((m) => m.CategoryFormComponent),
    title: 'Edit Expense Category | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expenseCategory.update])],
    data: { mode: 'edit' },
  },
];
