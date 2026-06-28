import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ThemeService } from '../../../core/theme/theme.service';
import { ThemeDefinition, ThemeId } from '../../../core/theme/theme.model';

@Component({
  selector: 'app-theme-selector',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './theme-selector.component.html',
  styleUrl: './theme-selector.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeSelectorComponent {
  private readonly themeService = inject(ThemeService);

  readonly searchQuery = signal('');
  readonly previewThemeId = signal<ThemeId | null>(null);

  readonly activeThemeId = this.themeService.themeId;
  readonly favoriteThemeIds = this.themeService.favoriteThemeIds;
  readonly recentThemeIds = this.themeService.recentThemeIds;

  readonly filteredThemes = computed(() => this.themeService.searchThemes(this.searchQuery()));

  readonly favoriteThemes = computed(() =>
    this.themeService.availableThemes().filter((t) => this.favoriteThemeIds().includes(t.id)),
  );

  readonly recentThemes = computed(() =>
    this.recentThemeIds()
      .map((id) => this.themeService.resolveThemeDefinition(id))
      .filter((t): t is ThemeDefinition => !!t),
  );

  readonly previewThemeDefinition = computed(() => {
    const previewId = this.previewThemeId();
    if (previewId) {
      return this.themeService.resolveThemeDefinition(previewId);
    }
    return this.themeService.activeTheme();
  });

  selectTheme(themeId: ThemeId): void {
    this.themeService.setThemeId(themeId);
    this.previewThemeId.set(null);
  }

  onPreviewTheme(themeId: ThemeId): void {
    this.previewThemeId.set(themeId);
    document.documentElement.setAttribute('data-theme-id', themeId);
  }

  cancelPreview(): void {
    this.previewThemeId.set(null);
    document.documentElement.setAttribute('data-theme-id', this.activeThemeId());
  }

  toggleFavorite(event: Event, themeId: ThemeId): void {
    event.stopPropagation();
    this.themeService.toggleFavorite(themeId);
  }

  isFavorite(themeId: ThemeId): boolean {
    return this.themeService.isFavorite(themeId);
  }

  isActive(themeId: ThemeId): boolean {
    return this.activeThemeId() === themeId;
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
  }
}
