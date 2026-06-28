import { Routes } from '@angular/router';

export const SALES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./sales-dashboard/sales-dashboard.component').then((m) => m.SalesDashboardComponent),
    title: 'Sales Dashboard | BusinessOS',
  },
];
