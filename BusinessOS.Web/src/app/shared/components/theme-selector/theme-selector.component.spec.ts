import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ThemeSelectorComponent } from './theme-selector.component';
import { ThemeService } from '../../../core/theme/theme.service';
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

describe('ThemeSelectorComponent', () => {
  let fixture: ComponentFixture<ThemeSelectorComponent>;
  let component: ThemeSelectorComponent;
  let themeService: ThemeService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ThemeSelectorComponent],
      providers: [
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
    fixture = TestBed.createComponent(ThemeSelectorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should filter themes by search query', () => {
    component.onSearchChange('high contrast');
    expect(component.filteredThemes().some((t) => t.id === 'high-contrast')).toBeTrue();
  });

  it('should select theme via service', () => {
    spyOn(themeService, 'setThemeId');
    component.selectTheme('green-business');
    expect(themeService.setThemeId).toHaveBeenCalledWith('green-business');
  });

  it('should toggle favorite without selecting theme', () => {
    spyOn(themeService, 'toggleFavorite');
    const event = new Event('click');
    spyOn(event, 'stopPropagation');
    component.toggleFavorite(event, 'light');
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(themeService.toggleFavorite).toHaveBeenCalledWith('light');
  });

  it('should preview theme on hover and restore on cancel', () => {
    themeService.setThemeId('light');
    component.onPreviewTheme('dark');
    expect(document.documentElement.getAttribute('data-theme-id')).toBe('dark');
    component.cancelPreview();
    expect(document.documentElement.getAttribute('data-theme-id')).toBe('light');
  });
});
