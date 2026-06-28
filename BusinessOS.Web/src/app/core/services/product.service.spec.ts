import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductService } from './product.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('ProductService', () => {
  let service: ProductService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ProductService],
    });
    service = TestBed.inject(ProductService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch products with filters', () => {
    service.getAll({ search: 'widget', categoryId: 'cat-1' }).subscribe((result) => {
      expect(result.items[0].name).toBe('Widget');
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.products));
    expect(req.request.params.get('search')).toBe('widget');
    req.flush({
      items: [{ id: '1', categoryId: 'cat-1', name: 'Widget', sku: 'W-1', costPrice: 10, salePrice: 20, currentStock: 5, reorderLevel: 2, isActive: true }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });
  });
});
