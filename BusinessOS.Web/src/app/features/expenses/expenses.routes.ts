import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const EXPENSE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./expense-list/expense-list.component').then((m) => m.ExpenseListComponent),
    title: 'Expenses | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expense.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./expense-form/expense-form.component').then((m) => m.ExpenseFormComponent),
    title: 'Create Expense | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expense.create])],
    data: { mode: 'create' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./expense-form/expense-form.component').then((m) => m.ExpenseFormComponent),
    title: 'Edit Expense | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expense.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./expense-detail/expense-detail.component').then((m) => m.ExpenseDetailComponent),
    title: 'Expense Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.expense.view])],
  },
];
