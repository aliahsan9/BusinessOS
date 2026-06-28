import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppearanceSettingsComponent } from './appearance-settings.component';
import { ThemeService } from '../../../core/theme/theme.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SettingsService } from '../../../core/services/settings.service';
import { TenantSettingsDto } from '../../../core/models/settings.model';
import { of } from 'rxjs';

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

describe('AppearanceSettingsComponent', () => {
  let fixture: ComponentFixture<AppearanceSettingsComponent>;
  let component: AppearanceSettingsComponent;
  let themeService: ThemeService;
  let notification: jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    localStorage.clear();
    notification = jasmine.createSpyObj('NotificationService', ['success', 'error', 'info', 'warning']);

    await TestBed.configureTestingModule({
      imports: [AppearanceSettingsComponent],
      providers: [
        provideRouter([]),
        { provide: NotificationService, useValue: notification },
        {
          provide: SettingsService,
          useValue: jasmine.createSpyObj('SettingsService', ['getSettings', 'updateSettings'], {
            getSettings: () => of(mockSettings()),
            updateSettings: () => of(mockSettings()),
          }),
        },
      ],
    }).compileComponents();

    themeService = TestBed.inject(ThemeService);
    themeService.initialize();
    fixture = TestBed.createComponent(AppearanceSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should update preferences through service', () => {
    spyOn(themeService, 'updatePreferences');
    component.updatePref({ fontFamily: 'nunito' });
    expect(themeService.updatePreferences).toHaveBeenCalledWith({ fontFamily: 'nunito' });
  });

  it('should toggle compact mode', () => {
    spyOn(themeService, 'updatePreferences');
    component.onCompactModeChange(true);
    expect(themeService.updatePreferences).toHaveBeenCalledWith({ compactMode: true });
  });

  it('should sync to backend on save', () => {
    spyOn(themeService, 'syncToBackend').and.returnValue(of(undefined));
    component.saveAndSync();
    expect(themeService.syncToBackend).toHaveBeenCalled();
    expect(notification.success).toHaveBeenCalled();
  });

  it('should export theme JSON', () => {
    spyOn(themeService, 'exportTheme').and.returnValue('{"version":1}');
    component.exportTheme();
    expect(themeService.exportTheme).toHaveBeenCalled();
    expect(notification.success).toHaveBeenCalled();
  });
});
