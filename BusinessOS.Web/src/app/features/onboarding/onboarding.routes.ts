import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { onboardingWizardGuard } from '../../core/guards/onboarding.guard';

export const ONBOARDING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./onboarding-wizard/onboarding-wizard.component').then((m) => m.OnboardingWizardComponent),
    canActivate: [authGuard, onboardingWizardGuard],
    title: 'Welcome | BusinessOS Onboarding',
  },
];
