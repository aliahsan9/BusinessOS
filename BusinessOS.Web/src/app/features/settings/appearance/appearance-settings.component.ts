import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  BORDER_RADIUS_SCALE,
  FONT_FAMILIES,
  FONT_SIZE_SCALE,
  ThemeService,
  THEME_CSS_TOKENS,
} from '../../../core/theme';
import {
  BorderRadiusId,
  CardStyle,
  ColorScheme,
  FontFamilyId,
  FontSizeId,
  FooterVisibility,
  NavbarStyle,
  SidebarStyle,
  TableDensity,
} from '../../../core/theme/theme.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { NotificationService } from '../../../core/services/notification.service';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { ThemeSelectorComponent } from '../../../shared/components/theme-selector/theme-selector.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-appearance-settings',
  standalone: true,
  imports: [
    FormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppButtonComponent,
    ThemeSelectorComponent,
  ],
  templateUrl: './appearance-settings.component.html',
  styleUrl: './appearance-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppearanceSettingsComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly ROUTES = ROUTES;
  readonly FONT_FAMILIES = FONT_FAMILIES;
  readonly FONT_SIZE_SCALE = FONT_SIZE_SCALE;
  readonly BORDER_RADIUS_SCALE = BORDER_RADIUS_SCALE;
  readonly THEME_CSS_TOKENS = THEME_CSS_TOKENS;

  private readonly themeService = inject(ThemeService);
  private readonly notification = inject(NotificationService);

  readonly preferences = this.themeService.preferences;
  readonly resolvedAppearance = this.themeService.resolvedAppearance;
  readonly saving = signal(false);
  readonly showBuilder = signal(false);

  readonly breadcrumbs = [
    { label: 'Settings', route: ROUTES.settings.hub },
    { label: 'Appearance' },
  ];

  readonly headerActions = [
    {
      label: '← Back to Settings',
      route: ROUTES.settings.hub,
      variant: ButtonVariant.Ghost,
    },
  ];

  readonly fontOptions = Object.entries(FONT_FAMILIES).map(([id, meta]) => ({
    id: id as FontFamilyId,
    label: meta.label,
  }));

  readonly fontSizeOptions: { id: FontSizeId; label: string }[] = [
    { id: 'sm', label: 'Small' },
    { id: 'md', label: 'Medium' },
    { id: 'lg', label: 'Large' },
    { id: 'xl', label: 'Extra Large' },
  ];

  readonly borderRadiusOptions: { id: BorderRadiusId; label: string }[] = [
    { id: 'none', label: 'None' },
    { id: 'sm', label: 'Small' },
    { id: 'md', label: 'Medium' },
    { id: 'lg', label: 'Large' },
    { id: 'full', label: 'Pill' },
  ];

  readonly sidebarOptions: { id: SidebarStyle; label: string }[] = [
    { id: 'expanded', label: 'Expanded' },
    { id: 'collapsed', label: 'Collapsed' },
    { id: 'mini', label: 'Mini' },
    { id: 'floating', label: 'Floating' },
  ];

  readonly navbarOptions: { id: NavbarStyle; label: string }[] = [
    { id: 'fixed', label: 'Fixed' },
    { id: 'static', label: 'Static' },
    { id: 'compact', label: 'Compact' },
  ];

  readonly footerOptions: { id: FooterVisibility; label: string }[] = [
    { id: 'visible', label: 'Visible' },
    { id: 'hidden', label: 'Hidden' },
  ];

  readonly cardStyleOptions: { id: CardStyle; label: string }[] = [
    { id: 'flat', label: 'Flat' },
    { id: 'elevated', label: 'Elevated' },
    { id: 'glassmorphism', label: 'Glassmorphism' },
    { id: 'modern', label: 'Modern' },
  ];

  readonly tableDensityOptions: { id: TableDensity; label: string }[] = [
    { id: 'compact', label: 'Compact' },
    { id: 'comfortable', label: 'Comfortable' },
    { id: 'spacious', label: 'Spacious' },
  ];

  readonly colorSchemeOptions: { id: ColorScheme; label: string }[] = [
    { id: 'light', label: 'Light' },
    { id: 'dark', label: 'Dark' },
    { id: 'system', label: 'System' },
  ];

  customThemeName = '';
  customPrimary = '#2563eb';
  customBackground = '#f9fafb';
  customSurface = '#ffffff';
  customText = '#111827';

  updatePref(partial: Parameters<ThemeService['updatePreferences']>[0]): void {
    this.themeService.updatePreferences(partial);
  }

  toggleDarkMode(): void {
    this.themeService.toggleDarkMode();
  }

  saveAndSync(): void {
    this.saving.set(true);
    this.themeService.syncToBackend().subscribe({
      next: () => {
        this.notification.success('Appearance settings saved.');
        this.saving.set(false);
      },
      error: () => {
        this.notification.error('Failed to save appearance settings.');
        this.saving.set(false);
      },
    });
  }

  resetTheme(): void {
    if (confirm('Reset all appearance settings to defaults?')) {
      this.themeService.resetToDefaults();
      this.notification.info('Appearance reset to defaults.');
    }
  }

  exportTheme(): void {
    const json = this.themeService.exportTheme();
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `businessos-theme-${Date.now()}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    this.notification.success('Theme exported.');
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      try {
        this.themeService.importTheme(reader.result as string);
        this.notification.success('Theme imported successfully.');
      } catch {
        this.notification.error('Invalid theme file.');
      }
    };
    reader.readAsText(file);
    input.value = '';
  }

  saveCustomTheme(): void {
    if (!this.customThemeName.trim()) {
      this.notification.warning('Enter a name for your custom theme.');
      return;
    }

    const theme = this.themeService.createCustomThemeFromBuilder(this.customThemeName.trim(), {
      [THEME_CSS_TOKENS.primary]: this.customPrimary,
      [THEME_CSS_TOKENS.background]: this.customBackground,
      [THEME_CSS_TOKENS.surface]: this.customSurface,
      [THEME_CSS_TOKENS.text]: this.customText,
      [THEME_CSS_TOKENS.border]: '#e5e7eb',
      [THEME_CSS_TOKENS.sidebarBackground]: this.customSurface,
      [THEME_CSS_TOKENS.navbarBackground]: this.customSurface,
    });

    this.themeService.setThemeId(theme.id);
    this.customThemeName = '';
    this.showBuilder.set(false);
    this.notification.success(`Custom theme "${theme.name}" created.`);
  }

  toggleSystemScheme(enabled: boolean): void {
    this.updatePref({ colorScheme: enabled ? 'system' : this.resolvedAppearance() });
  }

  onCompactModeChange(enabled: boolean): void {
    this.updatePref({ compactMode: enabled });
  }

  onAnimationsChange(enabled: boolean): void {
    this.updatePref({ animationsEnabled: enabled });
  }

  onLargeFontChange(enabled: boolean): void {
    this.updatePref({ largeFontMode: enabled });
  }

  onReducedMotionChange(enabled: boolean): void {
    this.updatePref({ reducedMotion: enabled });
  }

  toggleBuilder(): void {
    this.showBuilder.update((v) => !v);
  }
}
