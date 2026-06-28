import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RoleService } from './role.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('RoleService', () => {
  let service: RoleService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), RoleService],
    });
    service = TestBed.inject(RoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch roles', () => {
    service.getAll().subscribe((r) => expect(r[0].name).toBe('Admin'));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.roles));
    req.flush([{ id: '1', name: 'Admin', isActive: true, createdAt: '2026-01-01', permissions: [] }]);
  });
});
