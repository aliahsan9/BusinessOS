import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const ONBOARDING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./onboarding-shell/onboarding-shell.component').then((m) => m.OnboardingShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'business', pathMatch: 'full' },
      {
        path: 'business',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Business Information | BusinessOS',
        data: { step: 1, breadcrumb: 'Business Information' },
      },
      {
        path: 'owner',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Owner Information | BusinessOS',
        data: { step: 2, breadcrumb: 'Owner Information' },
      },
      {
        path: 'address',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Business Address | BusinessOS',
        data: { step: 3, breadcrumb: 'Business Address' },
      },
      {
        path: 'currency-tax',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Currency & Tax | BusinessOS',
        data: { step: 4, breadcrumb: 'Currency & Tax Settings' },
      },
      {
        path: 'invoice-preferences',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Invoice Preferences | BusinessOS',
        data: { step: 5, breadcrumb: 'Invoice Preferences' },
      },
      {
        path: 'logo',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Upload Logo | BusinessOS',
        data: { step: 6, breadcrumb: 'Logo Upload' },
      },
      {
        path: 'review',
        loadComponent: () =>
          import('./onboarding-placeholder/onboarding-placeholder.component').then(
            (m) => m.OnboardingPlaceholderComponent
          ),
        title: 'Review & Finish | BusinessOS',
        data: { step: 7, breadcrumb: 'Review & Finish' },
      },
    ],
  },
];
