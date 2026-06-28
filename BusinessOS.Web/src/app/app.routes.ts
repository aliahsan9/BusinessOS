import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { buildFeatureRoutes } from './app-feature.routes';
import { ROUTES } from './core/constants/route.constants';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: 'onboarding',
    loadChildren: () => import('./features/onboarding/onboarding.routes').then((m) => m.ONBOARDING_ROUTES),
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./shared/pages/forbidden-page/forbidden-page.component').then((m) => m.ForbiddenPageComponent),
    title: 'Access Denied | BusinessOS',
  },
  {
    path: 'not-found',
    loadComponent: () =>
      import('./shared/pages/not-found-page/not-found-page.component').then((m) => m.NotFoundPageComponent),
    title: 'Page Not Found | BusinessOS',
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
      ...buildFeatureRoutes(),
    ],
  },
  { path: '**', redirectTo: ROUTES.notFound.replace(/^\//, '') },
];
