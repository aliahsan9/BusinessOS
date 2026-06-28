import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TokenService } from '../services/token.service';
import { NotificationService } from '../services/notification.service';

export const permissionGuard = (requiredPermissions: string[], requireAll = false): CanActivateFn => {
  return () => {
    const tokenService = inject(TokenService);
    const router = inject(Router);
    const notificationService = inject(NotificationService);

    const hasAccess = requireAll
      ? tokenService.hasAllPermissions(requiredPermissions)
      : tokenService.hasAnyPermission(requiredPermissions);

    if (hasAccess) {
      return true;
    }

    notificationService.warning('Access denied', 'You do not have permission to view this page.');
    return router.createUrlTree(['/forbidden']);
  };
};

export const roleGuard = (requiredRoles: string[]): CanActivateFn => {
  return () => {
    const tokenService = inject(TokenService);
    const router = inject(Router);
    const notificationService = inject(NotificationService);

    const hasRole = requiredRoles.some((role) => tokenService.hasRole(role));

    if (hasRole) {
      return true;
    }

    notificationService.warning('Access denied', 'Your role does not allow access to this page.');
    return router.createUrlTree(['/dashboard']);
  };
};
