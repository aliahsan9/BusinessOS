import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FinanceService } from './finance.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('FinanceService', () => {
  let service: FinanceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), FinanceService],
    });
    service = TestBed.inject(FinanceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch dashboard', () => {
    service.getDashboard({ period: 'month' }).subscribe((r) => expect(r.netProfit).toBe(5000));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.finance.dashboard));
    req.flush({ totalRevenue: 10000, totalExpenses: 5000, netProfit: 5000, totalPayments: 8000, outstandingInvoices: 2000, completedOrders: 10, totalExpensesCount: 5, period: 'month', startDate: '2026-01-01', endDate: '2026-01-31', revenueTrend: [], expenseTrend: [] });
  });
});
