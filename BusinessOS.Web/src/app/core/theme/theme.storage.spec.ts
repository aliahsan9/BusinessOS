import { ThemeStorage } from './theme.storage';
import { DEFAULT_THEME_PREFERENCES } from './theme.model';
import { THEME_LEGACY_KEY, THEME_STORAGE_KEY } from './theme.constants';

describe('ThemeStorage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('should return defaults when storage is empty', () => {
    const prefs = ThemeStorage.load();
    expect(prefs).toEqual(jasmine.objectContaining({ version: 1, themeId: 'light' }));
  });

  it('should save and load preferences', () => {
    const prefs = { ...DEFAULT_THEME_PREFERENCES, themeId: 'dark' as const, colorScheme: 'dark' as const };
    ThemeStorage.save(prefs);
    const loaded = ThemeStorage.load();
    expect(loaded.themeId).toBe('dark');
    expect(loaded.colorScheme).toBe('dark');
  });

  it('should migrate legacy theme mode values', () => {
    localStorage.setItem(THEME_LEGACY_KEY, 'dark');
    const loaded = ThemeStorage.load();
    expect(loaded.themeId).toBe('dark');
    expect(loaded.colorScheme).toBe('dark');
  });

  it('should export and import preferences payload', () => {
    const prefs = { ...DEFAULT_THEME_PREFERENCES, themeId: 'blue-professional' as const };
    const exported = ThemeStorage.exportPreferences(prefs);
    const json = JSON.stringify(exported);
    const imported = ThemeStorage.importPreferences(json);
    expect(imported.themeId).toBe('blue-professional');
  });

  it('should throw on invalid import JSON', () => {
    expect(() => ThemeStorage.importPreferences('{"invalid":true}')).toThrowError(/Invalid theme export/);
  });

  it('should clear stored preferences', () => {
    ThemeStorage.save(DEFAULT_THEME_PREFERENCES);
    ThemeStorage.clear();
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
    expect(localStorage.getItem(THEME_LEGACY_KEY)).toBeNull();
  });
});
