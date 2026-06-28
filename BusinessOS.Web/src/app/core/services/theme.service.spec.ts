import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';
import { ThemeMode } from '../enums';
import { STORAGE_KEYS } from '../constants/storage.constants';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.classList.remove('theme-dark');
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should default to system mode', () => {
    expect(service.mode()).toBe(ThemeMode.System);
  });

  it('should set light mode and persist to storage', () => {
    service.setMode(ThemeMode.Light);
    expect(service.mode()).toBe(ThemeMode.Light);
    expect(service.resolvedTheme()).toBe('light');
    expect(localStorage.getItem(STORAGE_KEYS.theme)).toBe(ThemeMode.Light);
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('should set dark mode and apply data-theme attribute', () => {
    service.setMode(ThemeMode.Dark);
    expect(service.resolvedTheme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(document.documentElement.classList.contains('theme-dark')).toBeTrue();
  });

  it('should toggle between light and dark', () => {
    service.setMode(ThemeMode.Light);
    service.toggle();
    expect(service.mode()).toBe(ThemeMode.Dark);
    service.toggle();
    expect(service.mode()).toBe(ThemeMode.Light);
  });

  it('should persist mode to localStorage when set', () => {
    service.setMode(ThemeMode.Dark);
    expect(localStorage.getItem(STORAGE_KEYS.theme)).toBe(ThemeMode.Dark);
  });
});
