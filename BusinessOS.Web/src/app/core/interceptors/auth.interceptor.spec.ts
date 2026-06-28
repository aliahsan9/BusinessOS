import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { TokenService } from '../services/token.service';

describe('authInterceptor', () => {
  let httpMock: HttpTestingController;
  let httpClient: HttpClient;
  let tokenService: TokenService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    httpClient = TestBed.inject(HttpClient);
    tokenService = TestBed.inject(TokenService);
  });

  afterEach(() => httpMock.verify());

  it('should add authorization header when token exists', () => {
    tokenService.setSession('test-token', {
      userId: '1',
      email: 'a@b.com',
      tenantId: 't1',
      roles: [],
      permissions: [],
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    });

    httpClient.get('/api/dashboard/overview').subscribe();

    const req = httpMock.expectOne('/api/dashboard/overview');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    expect(req.request.headers.get('X-Tenant-ID')).toBe('t1');
    req.flush({});
  });
});
