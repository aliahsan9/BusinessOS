import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SettingsService } from './settings.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('SettingsService', () => {
  let service: SettingsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), SettingsService],
    });
    service = TestBed.inject(SettingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch settings', () => {
    service.getSettings().subscribe((r) => expect(r.currency).toBe('USD'));
    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.settings.base));
    req.flush({ id: '1', tenantId: 't1', currency: 'USD', language: 'en', taxRate: 0, theme: 'light', emailNotificationsEnabled: true, systemNotificationsEnabled: true, orderNotificationsEnabled: true, inventoryAlertsEnabled: true, paymentAlertsEnabled: true });
  });
});
