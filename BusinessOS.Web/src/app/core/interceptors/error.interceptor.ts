import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, finalize, throwError } from 'rxjs';
import { LoadingService } from '../services/loading.service';
import { NotificationService } from '../services/notification.service';
import { AuthService } from '../services/auth.service';
import { ApiError } from '../models/api-error.model';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notificationService = inject(NotificationService);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const apiError = toApiError(error);

      const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/register');
      const isPostLoginSync =
        req.url.includes('/settings') || req.url.includes('/onboarding/status');

      if (error.status === 401 && !isAuthEndpoint && !isPostLoginSync) {
        authService.logout(false);
        notificationService.error('Session expired', 'Please sign in again.');
      } else if (error.status === 403) {
        notificationService.error('Access denied', apiError.detail ?? 'You do not have permission for this action.');
      } else if (error.status === 0) {
        notificationService.error('Network error', 'Unable to reach the server. Check your connection.');
      } else if (error.status >= 500) {
        notificationService.error('Server error', apiError.detail ?? apiError.title);
      }

      return throwError(() => apiError);
    }),
  );
};

function toApiError(error: HttpErrorResponse): ApiError {
  if (error.status === 0) {
    return {
      status: 0,
      title: 'Cannot reach server',
      detail:
        'The API is unavailable. Start the backend with "dotnet run" in BusinessOS.API (http://localhost:5162), then try again.',
    };
  }

  if (error.error && typeof error.error === 'object') {
    const body = error.error as ApiError;
    if (body.status || body.title || body.detail) {
      return body;
    }
  }

  if (error.status === 500) {
    return {
      status: 500,
      title: 'Server unavailable',
      detail:
        'The API may not be running. Start the backend with "dotnet run" in BusinessOS.API (http://localhost:5162), then try again.',
    };
  }

  return {
    status: error.status,
    title: error.statusText || 'An unexpected error occurred',
    detail: error.message,
  };
}

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const loadingService = inject(LoadingService);
  const skipLoading = req.headers.has('X-Skip-Loading');

  if (!skipLoading) {
    loadingService.show();
  }

  return next(req).pipe(
    finalize(() => {
      if (!skipLoading) {
        loadingService.hide();
      }
    }),
  );
};
