import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { ThemeService } from './theme.service';
import { ThemeStorage } from './theme.storage';
import { THEME_STORAGE_KEY } from './theme.constants';
import { DEFAULT_THEME_PREFERENCES } from './theme.model';
import { SettingsService } from '../services/settings.service';
import { TenantSettingsDto } from '../models/settings.model';
import { ThemeMode } from '../enums';

const mockSettings = (overrides: Partial<TenantSettingsDto> = {}): TenantSettingsDto => ({
  id: 'settings-1',
  tenantId: 'tenant-1',
  currency: 'USD',
  language: 'en',
  taxRate: 0,
  invoicePrefix: null,
  emailFromAddress: null,
  theme: 'light',
  logoUrl: null,
  emailNotificationsEnabled: true,
  systemNotificationsEnabled: true,
  orderNotificationsEnabled: true,
  inventoryAlertsEnabled: true,
  paymentAlertsEnabled: true,
  ...overrides,
});

describe('ThemeService', () => {
  let service: ThemeService;
  let settingsService: jasmine.SpyObj<SettingsService>;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme-id');
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-bs-theme');
    document.documentElement.classList.remove('theme-dark');

    settingsService = jasmine.createSpyObj('SettingsService', ['getSettings', 'updateSettings']);
    settingsService.getSettings.and.returnValue(of(mockSettings()));
    settingsService.updateSettings.and.returnValue(of(mockSettings()));

    TestBed.configureTestingModule({
      providers: [{ provide: SettingsService, useValue: settingsService }],
    });
    service = TestBed.inject(ThemeService);
    service.initialize();
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme-id');
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-bs-theme');
    document.documentElement.classList.remove('theme-dark');
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should default to system color scheme', () => {
    expect(service.colorScheme()).toBe('system');
    expect(service.mode()).toBe(ThemeMode.System);
  });

  it('should apply theme id and appearance to document root', () => {
    service.setThemeId('green-business');
    service.setColorScheme('light');
    service.initialize();

    expect(document.documentElement.getAttribute('data-theme-id')).toBe('green-business');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    expect(document.documentElement.getAttribute('data-bs-theme')).toBe('light');
  });

  it('should toggle dark mode', () => {
    service.setColorScheme('light');
    service.toggleDarkMode();
    expect(service.resolvedAppearance()).toBe('dark');
    service.toggleDarkMode();
    expect(service.resolvedAppearance()).toBe('light');
  });

  it('should persist preferences to local storage', () => {
    service.setThemeId('purple-executive');
    service.updatePreferences({ fontFamily: 'poppins', compactMode: true });

    const stored = ThemeStorage.load();
    expect(stored.themeId).toBe('purple-executive');
    expect(stored.fontFamily).toBe('poppins');
    expect(stored.compactMode).toBeTrue();
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeTruthy();
  });

  it('should track recent themes', () => {
    service.setThemeId('light');
    service.setThemeId('dark');
    service.setThemeId('blue-professional');

    expect(service.recentThemeIds()[0]).toBe('blue-professional');
    expect(service.recentThemeIds()).toContain('dark');
  });

  it('should toggle favorites', () => {
    service.toggleFavorite('light');
    expect(service.isFavorite('light')).toBeTrue();
    service.toggleFavorite('light');
    expect(service.isFavorite('light')).toBeFalse();
  });

  it('should search themes by name and description', () => {
    const results = service.searchThemes('executive');
    expect(results.some((t) => t.id === 'purple-executive')).toBeTrue();
  });

  it('should export and import theme preferences', () => {
    service.setThemeId('orange-startup');
    service.updatePreferences({ fontSize: 'lg' });

    const exported = service.exportTheme();
    service.resetToDefaults();
    expect(service.themeId()).toBe('light');

    service.importTheme(exported);
    expect(service.themeId()).toBe('orange-startup');
    expect(service.preferences().fontSize).toBe('lg');
  });

  it('should reset to defaults', () => {
    service.setThemeId('dark');
    service.updatePreferences({ compactMode: true, largeFontMode: true });
    service.resetToDefaults();

    expect(service.themeId()).toBe(DEFAULT_THEME_PREFERENCES.themeId);
    expect(service.preferences().compactMode).toBeFalse();
    expect(service.preferences().largeFontMode).toBeFalse();
  });

  it('should apply layout preference attributes', () => {
    service.updatePreferences({
      sidebarStyle: 'floating',
      navbarStyle: 'compact',
      cardStyle: 'glassmorphism',
      tableDensity: 'compact',
      footerVisibility: 'hidden',
      animationsEnabled: false,
      largeFontMode: true,
      reducedMotion: true,
    });
    service.initialize();

    expect(document.documentElement.getAttribute('data-sidebar-style')).toBe('floating');
    expect(document.documentElement.getAttribute('data-navbar-style')).toBe('compact');
    expect(document.documentElement.hasAttribute('data-compact')).toBeFalse();
    expect(document.documentElement.hasAttribute('data-animations')).toBeFalse();
    expect(document.documentElement.hasAttribute('data-large-font')).toBeTrue();
    expect(document.documentElement.hasAttribute('data-reduced-motion')).toBeTrue();
  });

  it('should sync preferences from backend JSON theme field', fakeAsync(() => {
    settingsService.getSettings.and.returnValue(
      of(
        mockSettings({
          theme: JSON.stringify({ ...DEFAULT_THEME_PREFERENCES, themeId: 'corporate-gray', fontFamily: 'roboto' }),
        }),
      ),
    );

    service.syncFromBackend();
    tick();
    expect(service.themeId()).toBe('corporate-gray');
    expect(service.preferences().fontFamily).toBe('roboto');
  }));

  it('should create custom theme from builder', () => {
    const theme = service.createCustomThemeFromBuilder('Brand Theme', {
      '--primary-color': '#ff0000',
      '--background-color': '#ffffff',
    });

    expect(theme.id.startsWith('custom-')).toBeTrue();
    expect(service.preferences().customThemes.some((t) => t.id === theme.id)).toBeTrue();
  });
});
