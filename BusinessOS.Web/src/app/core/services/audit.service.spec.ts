import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditService } from './audit.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('AuditService', () => {
  let service: AuditService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), AuditService],
    });
    service = TestBed.inject(AuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch audit logs', () => {
    service.getAll({ action: 'Create' }).subscribe((r) => expect(r.items.length).toBe(1));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.auditLogs));
    req.flush({ items: [{ id: '1', actorUserId: 'u1', action: 'Create', entityType: 'Customer', entityId: 'c1', createdAt: '2026-01-01' }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false });
  });
});
