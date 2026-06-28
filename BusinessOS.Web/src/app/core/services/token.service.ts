import { Injectable, signal, computed } from '@angular/core';
import { AuthUser } from '../models/auth.model';
import { STORAGE_KEYS } from '../constants/storage.constants';
import { StorageHelper } from '../helpers/storage.helper';

@Injectable({ providedIn: 'root' })
export class TokenService {
  private readonly tokenSignal = signal<string | null>(this.loadToken());
  private readonly userSignal = signal<AuthUser | null>(this.loadUser());

  readonly token = this.tokenSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => !!this.tokenSignal() && !this.isTokenExpired());
  readonly permissions = computed(() => this.userSignal()?.permissions ?? []);
  readonly roles = computed(() => this.userSignal()?.roles ?? []);
  readonly tenantId = computed(() => this.userSignal()?.tenantId ?? null);

  setSession(token: string, user: AuthUser): void {
    StorageHelper.setString(STORAGE_KEYS.authToken, token);
    StorageHelper.set(STORAGE_KEYS.authUser, user);
    this.tokenSignal.set(token);
    this.userSignal.set(user);
  }

  clearSession(): void {
    StorageHelper.clear([STORAGE_KEYS.authToken, STORAGE_KEYS.authUser, STORAGE_KEYS.tenantId]);
    this.tokenSignal.set(null);
    this.userSignal.set(null);
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  getUser(): AuthUser | null {
    return this.userSignal();
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  hasAnyPermission(permissions: string[]): boolean {
    const userPermissions = this.permissions();
    return permissions.some((p) => userPermissions.includes(p));
  }

  hasAllPermissions(permissions: string[]): boolean {
    const userPermissions = this.permissions();
    return permissions.every((p) => userPermissions.includes(p));
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  isTokenExpired(): boolean {
    const user = this.userSignal();
    if (!user?.expiresAt) {
      return true;
    }
    return new Date(user.expiresAt).getTime() <= Date.now();
  }

  isTokenExpiringSoon(bufferMs = 60_000): boolean {
    const user = this.userSignal();
    if (!user?.expiresAt) {
      return true;
    }
    return new Date(user.expiresAt).getTime() - Date.now() <= bufferMs;
  }

  private loadToken(): string | null {
    return StorageHelper.getString(STORAGE_KEYS.authToken);
  }

  private loadUser(): AuthUser | null {
    return StorageHelper.get<AuthUser>(STORAGE_KEYS.authUser);
  }
}
