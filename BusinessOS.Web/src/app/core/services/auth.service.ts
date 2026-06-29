import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { TokenService } from './token.service';
import { NotificationService } from './notification.service';
import {
  AuthResponse,
  AuthUser,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  RememberMeCredentials,
  ResetPasswordRequest,
} from '../models/auth.model';
import { API_ENDPOINTS } from '../constants/api.constants';
import { STORAGE_KEYS } from '../constants/storage.constants';
import { StorageHelper } from '../helpers/storage.helper';
import { ApiError } from '../models/api-error.model';
import { ThemeService } from '../theme/theme.service';
import { TenantSettingsStoreService } from './tenant-settings-store.service';

@Injectable({ providedIn: 'root' })
export class AuthService extends BaseApiService {
  private readonly router = inject(Router);
  private readonly tokenService = inject(TokenService);
  private readonly notificationService = inject(NotificationService);
  private readonly themeService = inject(ThemeService);
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);

  private readonly loadingSignal = signal(false);
  readonly loading = this.loadingSignal.asReadonly();
  readonly currentUser = computed(() => this.tokenService.user());
  readonly isAuthenticated = computed(() => this.tokenService.isAuthenticated());

  login(request: LoginRequest, rememberMe = false): Observable<AuthResponse> {
    this.loadingSignal.set(true);
    return this.post<AuthResponse>(API_ENDPOINTS.auth.login, request).pipe(
      tap((response) => this.handleAuthSuccess(response, rememberMe ? request.email : undefined)),
      catchError((error: ApiError) => {
        this.loadingSignal.set(false);
        return throwError(() => error);
      }),
    );
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    this.loadingSignal.set(true);
    return this.post<AuthResponse>(API_ENDPOINTS.auth.register, request).pipe(
      tap((response) => this.handleAuthSuccess(response)),
      catchError((error: ApiError) => {
        this.loadingSignal.set(false);
        return throwError(() => error);
      }),
    );
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<{ message: string }> {
    return this.post<{ message: string }>(API_ENDPOINTS.auth.forgotPassword, request).pipe(
      catchError((error: ApiError) => {
        if (error.status === 404) {
          return throwError(() => ({
            ...error,
            detail: 'Password reset is not yet available. Please contact your administrator.',
          }));
        }
        return throwError(() => error);
      }),
    );
  }

  resetPassword(request: ResetPasswordRequest): Observable<{ message: string }> {
    return this.post<{ message: string }>(API_ENDPOINTS.auth.resetPassword, {
      email: request.email,
      token: request.token,
      newPassword: request.newPassword,
    });
  }

  logout(showNotification = true): void {
    this.tokenService.clearSession();
    this.tenantSettingsStore.clear();
    StorageHelper.remove(STORAGE_KEYS.rememberMe);
    if (showNotification) {
      this.notificationService.info('You have been signed out.');
    }
    void this.router.navigate(['/auth/login']);
  }

  getRememberMeEmail(): string | null {
    const credentials = StorageHelper.get<RememberMeCredentials>(STORAGE_KEYS.rememberMe);
    return credentials?.email ?? null;
  }

  refreshSessionIfNeeded(): boolean {
    if (!this.tokenService.isAuthenticated()) {
      this.logout(false);
      return false;
    }
    if (this.tokenService.isTokenExpiringSoon()) {
      this.notificationService.warning('Your session is expiring soon. Please sign in again.');
      this.logout(false);
      return false;
    }
    return true;
  }

  establishSession(response: AuthResponse, options?: { welcomeMessage?: string; rememberEmail?: string }): void {
    this.handleAuthSuccess(response, options?.rememberEmail, options?.welcomeMessage);
  }

  private handleAuthSuccess(
    response: AuthResponse,
    rememberEmail?: string,
    welcomeMessage = 'Welcome back!',
  ): void {
    const user: AuthUser = {
      userId: response.userId,
      email: response.email,
      tenantId: response.tenantId,
      roles: response.roles,
      permissions: response.permissions,
      expiresAt: response.expiresAt,
    };
    this.tokenService.setSession(response.token, user);
    StorageHelper.setString(STORAGE_KEYS.tenantId, response.tenantId);

    if (rememberEmail) {
      StorageHelper.set<RememberMeCredentials>(STORAGE_KEYS.rememberMe, { email: rememberEmail });
    } else {
      StorageHelper.remove(STORAGE_KEYS.rememberMe);
    }

    this.loadingSignal.set(false);
    this.notificationService.success(welcomeMessage);
    this.themeService.syncFromBackend();
    this.tenantSettingsStore.load().subscribe();
  }
}
