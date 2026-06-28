import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InvoiceService } from './invoice.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('InvoiceService', () => {
  let service: InvoiceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), InvoiceService],
    });
    service = TestBed.inject(InvoiceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch invoices', () => {
    service.getAll({ search: 'INV' }).subscribe((result) => {
      expect(result.items[0].invoiceNumber).toBe('INV-001');
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.invoices));
    req.flush({
      items: [{ id: '1', invoiceNumber: 'INV-001', orderId: 'o1', orderNumber: 'ORD-001', customerId: 'c1', customerName: 'Ali', invoiceDate: '2026-01-01', dueDate: '2026-02-01', status: 'Draft', grandTotal: 100, amountPaid: 0, outstandingAmount: 100, createdAt: '2026-01-01' }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    });
  });
});
