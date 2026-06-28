import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OrderService } from './order.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { OrderStatus } from '../enums';

describe('OrderService', () => {
  let service: OrderService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), OrderService],
    });
    service = TestBed.inject(OrderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch orders with status filter', () => {
    service.getAll({ status: OrderStatus.Pending }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.orders));
    expect(req.request.params.get('status')).toBe('Pending');
    req.flush({
      items: [{ id: '1', orderNumber: 'ORD-001', orderDate: '2026-01-01', createdAt: '2026-01-01', status: 'Pending', customerName: 'Ali', customerEmail: 'ali@test.com', grandTotal: 100 }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    });
  });
});
