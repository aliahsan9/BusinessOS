import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const SUBSCRIPTION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./subscription.component').then((m) => m.SubscriptionComponent),
    canActivate: [permissionGuard([PermissionCodes.settings.view])],
    title: 'Subscription | BusinessOS',
  },
];
