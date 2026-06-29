import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';

export const ACTIVITY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./activity-timeline/activity-timeline.component').then((m) => m.ActivityTimelineComponent),
    canActivate: [permissionGuard(['Activity.View'])],
    title: 'Activity | BusinessOS',
  },
];
