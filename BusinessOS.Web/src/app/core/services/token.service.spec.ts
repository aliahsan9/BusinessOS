import { TestBed } from '@angular/core/testing';
import { TokenService } from './token.service';
import { STORAGE_KEYS } from '../constants/storage.constants';
import { AuthUser } from '../models/auth.model';

describe('TokenService', () => {
  let service: TokenService;

  const mockUser: AuthUser = {
    userId: '1',
    email: 'test@example.com',
    tenantId: 'tenant-1',
    roles: ['Admin'],
    permissions: ['Order.View'],
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(TokenService);
  });

  afterEach(() => localStorage.clear());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should set and clear session', () => {
    service.setSession('token-123', mockUser);
    expect(service.getToken()).toBe('token-123');
    expect(service.getUser()?.email).toBe('test@example.com');
    service.clearSession();
    expect(service.getToken()).toBeNull();
  });

  it('should check permissions', () => {
    service.setSession('token', mockUser);
    expect(service.hasPermission('Order.View')).toBeTrue();
    expect(service.hasPermission('Order.Delete')).toBeFalse();
  });

  it('should detect expired token', () => {
    const expiredUser = { ...mockUser, expiresAt: new Date(Date.now() - 1000).toISOString() };
    service.setSession('token', expiredUser);
    expect(service.isTokenExpired()).toBeTrue();
  });
});
