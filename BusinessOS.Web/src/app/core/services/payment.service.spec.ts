import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PaymentService } from './payment.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('PaymentService', () => {
  let service: PaymentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PaymentService],
    });
    service = TestBed.inject(PaymentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch payments', () => {
    service.getAll({ customerId: 'c1' }).subscribe((result) => {
      expect(result.items[0].amount).toBe(50);
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.payments));
    req.flush({
      items: [{ id: '1', orderId: 'o1', orderNumber: 'ORD-001', customerId: 'c1', customerName: 'Ali', amount: 50, paymentMethod: 'Cash', paymentDate: '2026-01-01', createdAt: '2026-01-01' }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    });
  });
});
