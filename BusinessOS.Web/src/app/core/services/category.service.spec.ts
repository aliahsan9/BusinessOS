import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CategoryService } from './category.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('CategoryService', () => {
  let service: CategoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CategoryService],
    });
    service = TestBed.inject(CategoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch paginated categories', () => {
    service.getAll({ page: 1, pageSize: 20 }).subscribe((result) => {
      expect(result.items.length).toBe(1);
      expect(result.totalCount).toBe(1);
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.categories));
    expect(req.request.method).toBe('GET');
    req.flush({
      items: [{ id: '1', name: 'Electronics', description: null }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });
  });

  it('should create a category', () => {
    service.create({ name: 'Books' }).subscribe((res) => expect(res.id).toBeTruthy());

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.categories));
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'abc' });
  });
});
