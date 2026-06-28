import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomerService } from './customer.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('CustomerService', () => {
  let service: CustomerService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CustomerService],
    });
    service = TestBed.inject(CustomerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch customers with filters', () => {
    service.getAll({ search: 'Ali', city: 'Lahore' }).subscribe((result) => {
      expect(result.items[0].fullName).toBe('Ali Khan');
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.customers));
    expect(req.request.params.get('search')).toBe('Ali');
    req.flush({
      items: [{ id: '1', fullName: 'Ali Khan', email: 'ali@test.com', phoneNumber: '123', city: 'Lahore', country: 'PK', isActive: true, createdAt: '2026-01-01' }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    });
  });
});
