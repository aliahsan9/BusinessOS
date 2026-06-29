import { Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ThemeMode } from '../enums';
import { SettingsService } from '../services/settings.service';
import { catchError, map, Observable, of, switchMap, throwError } from 'rxjs';
import {
  BUILT_IN_THEMES,
  BUILT_IN_THEME_LIST,
  BORDER_RADIUS_SCALE,
  FONT_FAMILIES,
  FONT_SIZE_SCALE,
  MAX_RECENT_THEMES,
  THEME_LEGACY_KEY,
} from './theme.constants';
import {
  ColorScheme,
  DEFAULT_THEME_PREFERENCES,
  FontFamilyId,
  ThemeDefinition,
  ThemeId,
  ThemePreferences,
} from './theme.model';
import { LEGACY_TOKEN_ALIASES, THEME_CSS_TOKENS } from './theme.tokens';
import { ThemeStorage } from './theme.storage';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly settingsService = inject(SettingsService);
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  private readonly initialized = signal(false);
  private fontLinkEl: HTMLLinkElement | null = null;
  private customStyleEl: HTMLStyleElement | null = null;

  private readonly _preferences = signal<ThemePreferences>({ ...DEFAULT_THEME_PREFERENCES });

  readonly preferences = this._preferences.asReadonly();
  readonly themeId = computed(() => this._preferences().themeId);
  readonly colorScheme = computed(() => this._preferences().colorScheme);
  readonly availableThemes = computed(() => [...BUILT_IN_THEME_LIST, ...this._preferences().customThemes]);
  readonly activeTheme = computed(() => this.resolveThemeDefinition(this.themeId()));
  readonly favoriteThemeIds = computed(() => this._preferences().favoriteThemeIds);
  readonly recentThemeIds = computed(() => this._preferences().recentThemeIds);

  /** @deprecated Use colorScheme / resolvedAppearance instead */
  readonly mode = computed<ThemeMode>(() => {
    const scheme = this._preferences().colorScheme;
    if (scheme === 'dark') return ThemeMode.Dark;
    if (scheme === 'light') return ThemeMode.Light;
    return ThemeMode.System;
  });

  readonly resolvedAppearance = computed<'light' | 'dark'>(() => {
    const theme = this.activeTheme();
    if (theme?.isDark) {
      return 'dark';
    }
    const scheme = this._preferences().colorScheme;
    if (scheme === 'dark') return 'dark';
    if (scheme === 'light') return 'light';
    return this.getSystemPreference();
  });

  /** @deprecated Use resolvedAppearance */
  readonly resolvedTheme = this.resolvedAppearance;

  constructor() {
    if (this.isBrowser) {
      const prefs = ThemeStorage.load();
      this._preferences.set(prefs);

      effect(() => {
        this.applyAll(this._preferences(), this.resolvedAppearance());
      });

      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        if (this._preferences().colorScheme === 'system') {
          this.applyAppearance(this.resolvedAppearance());
        }
      });

      window.matchMedia('(prefers-reduced-motion: reduce)').addEventListener('change', () => {
        this.applyAccessibility(this._preferences());
      });
    }
  }

  initialize(): void {
    if (!this.isBrowser) return;
    this.applyAll(this._preferences(), this.resolvedAppearance());
    this.initialized.set(true);
  }

  ensureInitialized(): boolean {
    if (!this.initialized()) {
      this.initialize();
    }
    return true;
  }

  /** Load preferences from backend tenant settings after login */
  syncFromBackend(): void {
    if (!this.isBrowser) return;
    this.settingsService
      .getSettings()
      .pipe(catchError(() => of(null)))
      .subscribe((settings) => {
        if (!settings?.theme) return;
        const backendTheme = settings.theme.trim();
        if (this.isValidThemeId(backendTheme)) {
          this.setThemeId(backendTheme as ThemeId, { persist: false, trackRecent: false });
        } else {
          try {
            const parsed = JSON.parse(backendTheme) as Partial<ThemePreferences>;
            if (parsed.version === 1) {
              this.updatePreferences(parsed, { persist: true, syncBackend: false });
            }
          } catch {
            const migrated = backendTheme as ColorScheme;
            if (['light', 'dark', 'system'].includes(migrated)) {
              this.setColorScheme(migrated as ColorScheme, { persist: false });
            }
          }
        }
      });
  }

  /** Persist full preferences to backend via tenant settings */
  syncToBackend(): Observable<void> {
    if (!this.isBrowser) {
      return of(undefined);
    }

    const prefs = this._preferences();
    return this.settingsService.getSettings().pipe(
      switchMap((current) => {
        if (!current) {
          return throwError(() => new Error('Unable to load current settings.'));
        }

        return this.settingsService.updateSettings({
          currency: current.currency,
          language: current.language,
          taxRate: current.taxRate,
          invoicePrefix: current.invoicePrefix,
          emailFromAddress: current.emailFromAddress,
          theme: JSON.stringify(prefs),
          logoUrl: current.logoUrl,
          timezone: current.timezone ?? 'UTC',
          aiAssistantEnabled: current.aiAssistantEnabled ?? true,
          aiShowSuggestions: current.aiShowSuggestions ?? true,
          emailNotificationsEnabled: current.emailNotificationsEnabled,
          systemNotificationsEnabled: current.systemNotificationsEnabled,
          orderNotificationsEnabled: current.orderNotificationsEnabled,
          inventoryAlertsEnabled: current.inventoryAlertsEnabled,
          paymentAlertsEnabled: current.paymentAlertsEnabled,
          taskNotificationsEnabled: current.taskNotificationsEnabled,
          invoiceNotificationsEnabled: current.invoiceNotificationsEnabled,
          customerNotificationsEnabled: current.customerNotificationsEnabled,
          projectNotificationsEnabled: current.projectNotificationsEnabled,
        });
      }),
      map(() => undefined),
    );
  }

  setThemeId(themeId: ThemeId, options?: { persist?: boolean; trackRecent?: boolean; syncBackend?: boolean }): void {
    const { persist = true, trackRecent = true, syncBackend = false } = options ?? {};
    if (!this.resolveThemeDefinition(themeId)) return;

    const recent = trackRecent
      ? [themeId, ...this._preferences().recentThemeIds.filter((id) => id !== themeId)].slice(0, MAX_RECENT_THEMES)
      : this._preferences().recentThemeIds;

    this.updatePreferences({ themeId, recentThemeIds: recent }, { persist, syncBackend });
  }

  setColorScheme(scheme: ColorScheme, options?: { persist?: boolean; syncBackend?: boolean }): void {
    this.updatePreferences({ colorScheme: scheme }, options);
  }

  toggleDarkMode(): void {
    const next = this.resolvedAppearance() === 'dark' ? 'light' : 'dark';
    this.setColorScheme(next);
  }

  /** @deprecated Use setColorScheme */
  setMode(mode: ThemeMode): void {
    const scheme: ColorScheme =
      mode === ThemeMode.Dark ? 'dark' : mode === ThemeMode.Light ? 'light' : 'system';
    this.setColorScheme(scheme);
    if (this.isBrowser) {
      localStorage.setItem(THEME_LEGACY_KEY, mode);
    }
  }

  /** @deprecated Use toggleDarkMode */
  toggle(): void {
    this.toggleDarkMode();
  }

  updatePreferences(
    partial: Partial<ThemePreferences>,
    options?: { persist?: boolean; syncBackend?: boolean },
  ): void {
    const { persist = true, syncBackend = false } = options ?? {};
    this._preferences.update((current) => ({ ...current, ...partial }));
    if (persist && this.isBrowser) {
      ThemeStorage.save(this._preferences());
    }
    if (syncBackend) {
      this.syncToBackend().subscribe();
    }
  }

  toggleFavorite(themeId: ThemeId): void {
    const favorites = this._preferences().favoriteThemeIds;
    const next = favorites.includes(themeId)
      ? favorites.filter((id) => id !== themeId)
      : [...favorites, themeId];
    this.updatePreferences({ favoriteThemeIds: next });
  }

  isFavorite(themeId: ThemeId): boolean {
    return this._preferences().favoriteThemeIds.includes(themeId);
  }

  searchThemes(query: string): ThemeDefinition[] {
    const q = query.trim().toLowerCase();
    if (!q) return this.availableThemes();
    return this.availableThemes().filter(
      (t) => t.name.toLowerCase().includes(q) || t.description.toLowerCase().includes(q),
    );
  }

  exportTheme(): string {
    return JSON.stringify(ThemeStorage.exportPreferences(this._preferences()), null, 2);
  }

  importTheme(json: string): void {
    const prefs = ThemeStorage.importPreferences(json);
    this._preferences.set(prefs);
    ThemeStorage.save(prefs);
    this.applyAll(prefs, this.resolvedAppearance());
  }

  resetToDefaults(): void {
    this._preferences.set({ ...DEFAULT_THEME_PREFERENCES });
    ThemeStorage.save(this._preferences());
    this.removeCustomStyle();
    this.applyAll(this._preferences(), this.resolvedAppearance());
  }

  saveCustomTheme(theme: ThemeDefinition): void {
    const customThemes = [
      ...this._preferences().customThemes.filter((t) => t.id !== theme.id),
      { ...theme, isBuiltIn: false, category: 'custom' as const },
    ];
    this.updatePreferences({ customThemes });
  }

  deleteCustomTheme(themeId: ThemeId): void {
    this.updatePreferences({
      customThemes: this._preferences().customThemes.filter((t) => t.id !== themeId),
    });
    if (this.themeId() === themeId) {
      this.setThemeId('light');
    }
  }

  createCustomThemeFromBuilder(name: string, tokens: Record<string, string>): ThemeDefinition {
    const id = `custom-${Date.now()}` as ThemeId;
    const theme: ThemeDefinition = {
      id,
      name,
      description: 'User-created custom theme',
      category: 'custom',
      preview: {
        primary: tokens[THEME_CSS_TOKENS.primary] ?? '#2563eb',
        secondary: tokens[THEME_CSS_TOKENS.secondary] ?? '#64748b',
        background: tokens[THEME_CSS_TOKENS.background] ?? '#f9fafb',
        surface: tokens[THEME_CSS_TOKENS.surface] ?? '#ffffff',
      },
      isDark: false,
      isBuiltIn: false,
      tokens,
    };
    this.saveCustomTheme(theme);
    return theme;
  }

  resolveThemeDefinition(themeId: ThemeId): ThemeDefinition | undefined {
    if (themeId in BUILT_IN_THEMES) {
      return BUILT_IN_THEMES[themeId as keyof typeof BUILT_IN_THEMES];
    }
    return this._preferences().customThemes.find((t) => t.id === themeId);
  }

  private applyAll(prefs: ThemePreferences, appearance: 'light' | 'dark'): void {
    if (!this.isBrowser) return;
    this.applyThemeId(prefs.themeId);
    this.applyAppearance(appearance);
    this.applyTypography(prefs);
    this.applyLayout(prefs);
    this.applyAccessibility(prefs);
    this.applyCustomTokens(prefs.themeId);
  }

  private applyThemeId(themeId: ThemeId): void {
    document.documentElement.setAttribute('data-theme-id', themeId);
  }

  private applyAppearance(appearance: 'light' | 'dark'): void {
    document.documentElement.setAttribute('data-theme', appearance);
    document.documentElement.setAttribute('data-bs-theme', appearance);
    document.documentElement.classList.toggle('theme-dark', appearance === 'dark');
  }

  private applyTypography(prefs: ThemePreferences): void {
    const font = FONT_FAMILIES[prefs.fontFamily];
    document.documentElement.style.setProperty('--font-family', font.stack);
    document.documentElement.style.setProperty('--font-size-base', FONT_SIZE_SCALE[prefs.fontSize]);

    const radius = BORDER_RADIUS_SCALE[prefs.borderRadius];
    document.documentElement.style.setProperty('--radius-sm', radius.sm);
    document.documentElement.style.setProperty('--radius-md', radius.md);
    document.documentElement.style.setProperty('--radius-lg', radius.lg);

    this.loadFont(font.googleUrl);
  }

  private applyLayout(prefs: ThemePreferences): void {
    const root = document.documentElement;
    root.setAttribute('data-sidebar-style', prefs.sidebarStyle);
    root.setAttribute('data-navbar-style', prefs.navbarStyle);
    root.setAttribute('data-card-style', prefs.cardStyle);
    root.setAttribute('data-table-density', prefs.tableDensity);
    root.setAttribute('data-footer-visibility', prefs.footerVisibility);
    root.toggleAttribute('data-compact', prefs.compactMode);
    root.toggleAttribute('data-animations', prefs.animationsEnabled);
  }

  private applyAccessibility(prefs: ThemePreferences): void {
    const root = document.documentElement;
    const systemReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const reducedMotion = prefs.reducedMotion || systemReducedMotion;
    root.toggleAttribute('data-reduced-motion', reducedMotion);
    root.toggleAttribute('data-large-font', prefs.largeFontMode);
  }

  private applyCustomTokens(themeId: ThemeId): void {
    const theme = this.resolveThemeDefinition(themeId);
    if (!theme?.tokens || theme.isBuiltIn) {
      this.removeCustomStyle();
      return;
    }

    if (!this.customStyleEl) {
      this.customStyleEl = document.createElement('style');
      this.customStyleEl.id = 'bos-custom-theme';
      document.head.appendChild(this.customStyleEl);
    }

    const vars = Object.entries(theme.tokens)
      .map(([key, value]) => `${key}: ${value};`)
      .join('\n  ');
    this.customStyleEl.textContent = `:root[data-theme-id="${themeId}"] {\n  ${vars}\n}`;
    this.syncLegacyAliases(theme.tokens);
  }

  private syncLegacyAliases(tokens: Record<string, string>): void {
    const root = document.documentElement;
    Object.entries(LEGACY_TOKEN_ALIASES).forEach(([legacy, canonical]) => {
      const value = tokens[canonical];
      if (value) {
        root.style.setProperty(legacy, value);
      }
    });
  }

  private removeCustomStyle(): void {
    this.customStyleEl?.remove();
    this.customStyleEl = null;
  }

  private loadFont(url: string): void {
    if (!this.fontLinkEl) {
      this.fontLinkEl = document.createElement('link');
      this.fontLinkEl.rel = 'stylesheet';
      document.head.appendChild(this.fontLinkEl);
    }
    if (this.fontLinkEl.href !== url) {
      this.fontLinkEl.href = url;
    }
  }

  private getSystemPreference(): 'light' | 'dark' {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private isValidThemeId(id: string): boolean {
    return id in BUILT_IN_THEMES || id.startsWith('custom-');
  }
}

/** Type alias for font selection in UI */
export type { FontFamilyId };
