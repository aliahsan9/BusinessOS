import { Routes } from '@angular/router';
import { permissionGuard } from './core/guards/permission.guard';
import { APP_ROUTE_PATHS, NAV_ITEMS } from './shared/constants/nav.constants';

function pathFromRoute(route: string): string {
  return route.replace(/^\//, '');
}

export function buildFeatureRoutes(): Routes {
  const placeholderRoutes = NAV_ITEMS.filter((item) => item.route !== APP_ROUTE_PATHS.dashboard).map((item) => ({
    path: pathFromRoute(item.route),
    loadComponent: () =>
      import('./shared/pages/feature-page/feature-page.component').then((m) => m.FeaturePageComponent),
    title: `${item.label} | BusinessOS`,
    data: {
      pageTitle: item.label,
      description: item.description,
      icon: item.icon,
    },
    ...(item.permissions?.length
      ? { canActivate: [permissionGuard(item.permissions, false)] }
      : {}),
  }));

  return [
    ...placeholderRoutes,
    {
      path: pathFromRoute(APP_ROUTE_PATHS.profile),
      loadComponent: () =>
        import('./shared/pages/feature-page/feature-page.component').then((m) => m.FeaturePageComponent),
      title: 'Profile | BusinessOS',
      data: {
        pageTitle: 'Profile',
        description: 'View and update your account profile.',
        icon: '👤',
      },
    },
  ];
}
