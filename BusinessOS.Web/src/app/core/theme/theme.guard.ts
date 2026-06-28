import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { ThemeService } from './theme.service';

/** Ensures theme preferences are loaded and applied before route activation */
export const themeGuard: CanActivateFn = () => {
  const themeService = inject(ThemeService);
  return themeService.ensureInitialized();
};
