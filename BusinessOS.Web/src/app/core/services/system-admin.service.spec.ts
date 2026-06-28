import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SystemAdminService } from './system-admin.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('SystemAdminService', () => {
  let service: SystemAdminService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), SystemAdminService],
    });
    service = TestBed.inject(SystemAdminService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch health', () => {
    service.getHealth().subscribe((r) => expect(r.status).toBe('Healthy'));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.system.health));
    req.flush({ status: 'Healthy', databaseConnected: true, checkedAt: '2026-01-01', checks: [] });
  });
});
