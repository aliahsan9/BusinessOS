import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NotificationCenterService } from './notification-center.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('NotificationCenterService', () => {
  let service: NotificationCenterService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), NotificationCenterService],
    });
    service = TestBed.inject(NotificationCenterService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch notifications', () => {
    service.getAll({ unreadOnly: true }).subscribe((r) => expect(r.items.length).toBe(1));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.notifications.base));
    req.flush({ items: [{ id: '1', userId: 'u1', title: 'Alert', message: 'Test', type: 'Info', isRead: false, createdAt: '2026-01-01' }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false });
  });
});
