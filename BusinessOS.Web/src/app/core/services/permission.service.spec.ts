import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PermissionService } from './permission.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('PermissionService', () => {
  let service: PermissionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PermissionService],
    });
    service = TestBed.inject(PermissionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch permissions', () => {
    service.getAll().subscribe((r) => expect(r[0].code).toBe('User.View'));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.permissions));
    req.flush([{ id: '1', name: 'View Users', code: 'User.View', category: 'User' }]);
  });
});
