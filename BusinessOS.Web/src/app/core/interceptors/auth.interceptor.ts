import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TokenService } from '../services/token.service';
import { HTTP_HEADERS } from '../constants/api.constants';
import { STORAGE_KEYS } from '../constants/storage.constants';
import { StorageHelper } from '../helpers/storage.helper';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const token = tokenService.getToken();
  const tenantId = tokenService.tenantId() ?? StorageHelper.getString(STORAGE_KEYS.tenantId);

  if (!token || req.url.includes('/auth/')) {
    return next(req);
  }

  const headers: Record<string, string> = {
    [HTTP_HEADERS.authorization]: `Bearer ${token}`,
  };

  if (tenantId) {
    headers[HTTP_HEADERS.tenantId] = tenantId;
  }

  return next(req.clone({ setHeaders: headers }));
};
