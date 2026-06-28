import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExpenseService } from './expense.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('ExpenseService', () => {
  let service: ExpenseService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ExpenseService],
    });
    service = TestBed.inject(ExpenseService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch expenses', () => {
    service.getAll({ search: 'rent' }).subscribe((result) => {
      expect(result.items[0].title).toBe('Office Rent');
    });
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.expenses.base));
    expect(req.request.params.get('search')).toBe('rent');
    req.flush({
      items: [{ id: '1', title: 'Office Rent', amount: 1000, expenseDate: '2026-01-01', categoryName: 'Rent', paymentMethod: 'Cash', status: 'Paid', isRecurring: true, createdAt: '2026-01-01' }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    });
  });
});
