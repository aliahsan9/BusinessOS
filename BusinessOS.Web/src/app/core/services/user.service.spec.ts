import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UserService } from './user.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('UserService', () => {
  let service: UserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), UserService],
    });
    service = TestBed.inject(UserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch users', () => {
    service.getAll({ search: 'admin' }).subscribe((r) => expect(r.items[0].fullName).toBe('Admin User'));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.users));
    req.flush({ items: [{ id: '1', email: 'admin@test.com', fullName: 'Admin User', isActive: true, roles: ['Admin'] }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false });
  });
});
