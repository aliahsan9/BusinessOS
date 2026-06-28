import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NAV_ITEMS } from '../../constants/nav.constants';
import { TokenService } from '../../../core/services/token.service';
import { ThemeService } from '../../../core/theme/theme.service';
import { STORAGE_KEYS } from '../../../core/constants/storage.constants';
import { StorageHelper } from '../../../core/helpers/storage.helper';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-sidebar.component.html',
  styleUrl: './app-sidebar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppSidebarComponent {
  private readonly tokenService = inject(TokenService);
  private readonly themeService = inject(ThemeService);

  readonly mobileOpen = input(false);
  readonly collapsed = signal(StorageHelper.get<boolean>(STORAGE_KEYS.sidebarCollapsed) ?? false);

  readonly mobileClose = output<void>();

  readonly isCollapsedView = computed(() => {
    const style = this.themeService.preferences().sidebarStyle;
    return this.collapsed() || style === 'collapsed' || style === 'mini';
  });

  readonly hideLabels = computed(() => {
    const style = this.themeService.preferences().sidebarStyle;
    return this.isCollapsedView() || style === 'mini';
  });

  readonly navItems = NAV_ITEMS.filter((item) => {
    if (!item.permissions?.length) {
      return true;
    }
    return this.tokenService.hasAnyPermission(item.permissions);
  });

  toggleCollapse(): void {
    this.collapsed.update((v) => !v);
    StorageHelper.set(STORAGE_KEYS.sidebarCollapsed, this.collapsed());
    this.themeService.updatePreferences({
      sidebarStyle: this.collapsed() ? 'collapsed' : 'expanded',
    });
  }

  onNavClick(): void {
    this.mobileClose.emit();
  }
}
