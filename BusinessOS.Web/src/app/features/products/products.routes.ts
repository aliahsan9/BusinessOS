import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const PRODUCT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./product-list/product-list.component').then((m) => m.ProductListComponent),
    title: 'Products | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.product.view])],
  },
  {
    path: 'new',
    loadComponent: () => import('./product-form/product-form.component').then((m) => m.ProductFormComponent),
    title: 'Create Product | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.product.create])],
    data: { mode: 'create' },
  },
  {
    path: 'categories',
    loadComponent: () => import('./category-list/category-list.component').then((m) => m.CategoryListComponent),
    title: 'Categories | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.category.view])],
  },
  {
    path: 'categories/new',
    loadComponent: () => import('./category-form/category-form.component').then((m) => m.CategoryFormComponent),
    title: 'Create Category | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.category.create])],
    data: { mode: 'create' },
  },
  {
    path: 'categories/:id/edit',
    loadComponent: () => import('./category-form/category-form.component').then((m) => m.CategoryFormComponent),
    title: 'Edit Category | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.category.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./product-form/product-form.component').then((m) => m.ProductFormComponent),
    title: 'Edit Product | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.product.update])],
    data: { mode: 'edit' },
  },
  {
    path: ':id',
    loadComponent: () => import('./product-detail/product-detail.component').then((m) => m.ProductDetailComponent),
    title: 'Product Details | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.product.view])],
  },
];
