import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./settings-hub/settings-hub.component').then((m) => m.SettingsHubComponent),
    title: 'Settings | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.settings.view])],
  },
];
