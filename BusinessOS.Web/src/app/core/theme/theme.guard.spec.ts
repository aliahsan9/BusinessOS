import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { themeGuard } from './theme.guard';
import { ThemeService } from './theme.service';
import { SettingsService } from '../services/settings.service';
import { TenantSettingsDto } from '../models/settings.model';

const mockSettings = (): TenantSettingsDto => ({
  id: 'settings-1',
  tenantId: 'tenant-1',
  currency: 'USD',
  language: 'en',
  taxRate: 0,
  theme: 'light',
  emailNotificationsEnabled: true,
  systemNotificationsEnabled: true,
  orderNotificationsEnabled: true,
  inventoryAlertsEnabled: true,
  paymentAlertsEnabled: true,
});

describe('themeGuard', () => {
  it('should ensure theme service is initialized', () => {
    const settingsService = jasmine.createSpyObj('SettingsService', ['getSettings', 'updateSettings']);
    settingsService.getSettings.and.returnValue(of(mockSettings()));
    settingsService.updateSettings.and.returnValue(of(mockSettings()));

    TestBed.configureTestingModule({
      providers: [{ provide: SettingsService, useValue: settingsService }],
    });

    const themeService = TestBed.inject(ThemeService);
    spyOn(themeService, 'ensureInitialized').and.returnValue(true);

    const result = TestBed.runInInjectionContext(() => themeGuard({} as never, {} as never));

    expect(themeService.ensureInitialized).toHaveBeenCalled();
    expect(result).toBeTrue();
  });
});
