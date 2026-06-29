import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionCodes } from '../../core/constants/permission.constants';

export const TEAM_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./team-page/team-page.component').then((m) => m.TeamPageComponent),
    title: 'Team | BusinessOS',
    canActivate: [permissionGuard([PermissionCodes.team.view])],
  },
];
