import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const NOTIFICATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./notification-list/notification-list.component').then((m) => m.NotificationListComponent),
    title: 'Notifications | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.notification.view])],
  },
  {
    path: 'settings',
    loadComponent: () =>
      import('./notification-settings/notification-settings.component').then((m) => m.NotificationSettingsComponent),
    title: 'Notification Settings | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.notification.update])],
  },
];
