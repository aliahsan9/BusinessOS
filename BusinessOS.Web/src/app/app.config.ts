import { ApplicationConfig, APP_INITIALIZER, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor, loadingInterceptor } from './core/interceptors/error.interceptor';
import { ThemeService } from './core/services/theme.service';

function initializeTheme(themeService: ThemeService): () => void {
  return () => themeService.initialize();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    provideHttpClient(withInterceptors([authInterceptor, loadingInterceptor, errorInterceptor])),
    provideAnimations(),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeTheme,
      deps: [ThemeService],
      multi: true,
    },
  ],
};
