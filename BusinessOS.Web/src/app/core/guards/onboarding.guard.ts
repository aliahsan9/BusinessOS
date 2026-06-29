import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError, of } from 'rxjs';
import { OnboardingService } from '../services/onboarding.service';
import { ROUTES } from '../constants/route.constants';

export const onboardingRedirectGuard: CanActivateFn = () => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    map((status) => {
      if (status.shouldShowWizard) {
        return router.createUrlTree([ROUTES.onboarding.base]);
      }
      return true;
    }),
    catchError(() => of(true)),
  );
};

export const onboardingWizardGuard: CanActivateFn = () => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    map((status) => {
      if (status.isCompleted) {
        return router.createUrlTree([ROUTES.dashboard]);
      }
      return true;
    }),
    catchError(() => of(true)),
  );
};
