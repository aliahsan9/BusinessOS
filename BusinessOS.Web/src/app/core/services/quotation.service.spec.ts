import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QuotationService } from './quotation.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('QuotationService', () => {
  let service: QuotationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), QuotationService],
    });
    service = TestBed.inject(QuotationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch quotations', () => {
    service.getAll({ search: 'QUO' }).subscribe((result) => {
      expect(result.items[0].quotationNumber).toBe('QUO-001');
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.quotations));
    req.flush({
      items: [{ id: '1', quotationNumber: 'QUO-001', customerId: 'c1', customerName: 'Ali', quotationDate: '2026-01-01', expiryDate: '2026-02-01', status: 'Draft', grandTotal: 500, createdAt: '2026-01-01' }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    });
  });
});
