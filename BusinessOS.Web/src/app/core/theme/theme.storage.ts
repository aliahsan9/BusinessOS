import { StorageHelper } from '../helpers/storage.helper';
import { STORAGE_KEYS } from '../constants/storage.constants';
import {
  DEFAULT_THEME_PREFERENCES,
  ThemeExportPayload,
  ThemePreferences,
} from './theme.model';
import { THEME_LEGACY_KEY, migrateLegacyThemeMode } from './theme.constants';

export class ThemeStorage {
  static load(): ThemePreferences {
    const stored = StorageHelper.get<ThemePreferences>(STORAGE_KEYS.themePreferences);
    if (stored?.version === 1) {
      return { ...DEFAULT_THEME_PREFERENCES, ...stored };
    }

    const legacy = localStorage.getItem(THEME_LEGACY_KEY);
    if (legacy) {
      const migrated = migrateLegacyThemeMode(legacy);
      return { ...DEFAULT_THEME_PREFERENCES, ...migrated };
    }

    return { ...DEFAULT_THEME_PREFERENCES };
  }

  static save(preferences: ThemePreferences): void {
    StorageHelper.set(STORAGE_KEYS.themePreferences, preferences);
  }

  static exportPreferences(preferences: ThemePreferences): ThemeExportPayload {
    return {
      exportedAt: new Date().toISOString(),
      preferences,
      customThemes: preferences.customThemes,
    };
  }

  static importPreferences(json: string): ThemePreferences {
    const parsed = JSON.parse(json) as ThemeExportPayload | ThemePreferences;
    if ('preferences' in parsed && parsed.preferences?.version === 1) {
      return { ...DEFAULT_THEME_PREFERENCES, ...parsed.preferences };
    }
    if ('version' in parsed && parsed.version === 1) {
      return { ...DEFAULT_THEME_PREFERENCES, ...parsed };
    }
    throw new Error('Invalid theme export file.');
  }

  static clear(): void {
    StorageHelper.remove(STORAGE_KEYS.themePreferences);
    localStorage.removeItem(THEME_LEGACY_KEY);
  }
}
