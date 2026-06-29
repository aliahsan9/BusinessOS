import { Routes } from '@angular/router';

export const HELP_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./help-center-page/help-center-page.component').then((m) => m.HelpCenterPageComponent),
    title: 'Help Center | BusinessOS',
  },
];
