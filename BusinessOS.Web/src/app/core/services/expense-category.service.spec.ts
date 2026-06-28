import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExpenseCategoryService } from './expense-category.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('ExpenseCategoryService', () => {
  let service: ExpenseCategoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ExpenseCategoryService],
    });
    service = TestBed.inject(ExpenseCategoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch categories', () => {
    service.getAll(true).subscribe((result) => expect(result.length).toBe(1));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.expenseCategories));
    req.flush([{ id: '1', name: 'Utilities', isActive: true, expenseCount: 0, totalAmount: 0, createdAt: '2026-01-01' }]);
  });
});
