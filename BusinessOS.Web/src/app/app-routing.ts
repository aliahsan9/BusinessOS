import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const APP_ROUTES: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    loadComponent: () =>
      import('./shared/layouts/dashboard-layout/dashboard-layout.component').then((m) => m.DashboardLayoutComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      // Feature modules will be added in subsequent phases
      { path: 'users', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'roles', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'permissions', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'customers', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'products', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'inventory', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'orders', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'invoices', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'reports', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'settings', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
