import { Injectable, signal, computed, effect, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ThemeMode } from '../enums';
import { STORAGE_KEYS } from '../constants/storage.constants';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  private readonly _mode = signal<ThemeMode>(ThemeMode.System);

  readonly mode = this._mode.asReadonly();

  readonly resolvedTheme = computed<'light' | 'dark'>(() => {
    const mode = this._mode();
    if (mode === ThemeMode.System) {
      return this.getSystemPreference();
    }
    return mode === ThemeMode.Dark ? 'dark' : 'light';
  });

  constructor() {
    if (this.isBrowser) {
      const stored = localStorage.getItem(STORAGE_KEYS.theme) as ThemeMode | null;
      if (stored && Object.values(ThemeMode).includes(stored)) {
        this._mode.set(stored);
      }

      effect(() => {
        const theme = this.resolvedTheme();
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.classList.toggle('theme-dark', theme === 'dark');
      });

      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        if (this._mode() === ThemeMode.System) {
          this.applyTheme();
        }
      });
    }
  }

  setMode(mode: ThemeMode): void {
    this._mode.set(mode);
    if (this.isBrowser) {
      localStorage.setItem(STORAGE_KEYS.theme, mode);
      this.applyTheme();
    }
  }

  toggle(): void {
    const next = this.resolvedTheme() === 'dark' ? ThemeMode.Light : ThemeMode.Dark;
    this.setMode(next);
  }

  initialize(): void {
    this.applyTheme();
  }

  private applyTheme(): void {
    if (!this.isBrowser) {
      return;
    }
    const theme = this.resolvedTheme();
    document.documentElement.setAttribute('data-theme', theme);
    document.documentElement.classList.toggle('theme-dark', theme === 'dark');
  }

  private getSystemPreference(): 'light' | 'dark' {
    if (!this.isBrowser) {
      return 'light';
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
