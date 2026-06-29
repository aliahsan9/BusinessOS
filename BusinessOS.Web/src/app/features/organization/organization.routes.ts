import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const ORGANIZATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./organization-settings/organization-settings.component').then(
        (m) => m.OrganizationSettingsComponent,
      ),
    title: 'Organization | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.organization.view])],
  },
];
