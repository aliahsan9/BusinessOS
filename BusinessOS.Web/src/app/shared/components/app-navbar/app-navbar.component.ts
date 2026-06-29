import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, output, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { TokenService } from '../../../core/services/token.service';
import { TenantSettingsStoreService } from '../../../core/services/tenant-settings-store.service';
import { NotificationStateService } from '../../../state/notification.state';
import { ThemeService } from '../../../core/theme/theme.service';
import { APP_ROUTE_PATHS, TOP_NAV_ITEMS } from '../../constants/nav.constants';
import { ROUTES } from '../../../core/constants/route.constants';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NotificationBellComponent],
  templateUrl: './app-navbar.component.html',
  styleUrl: './app-navbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppNavbarComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly tokenService = inject(TokenService);
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);
  private readonly notificationState = inject(NotificationStateService);
  private readonly themeService = inject(ThemeService);

  readonly menuToggle = output<void>();
  readonly routes = APP_ROUTE_PATHS;
  readonly settingsRoutes = ROUTES;

  readonly resolvedAppearance = this.themeService.resolvedAppearance;
  readonly isDarkMode = computed(() => this.resolvedAppearance() === 'dark');

  readonly navItems = TOP_NAV_ITEMS.filter((item) => {
    if (!item.permissions?.length) {
      return true;
    }
    return this.tokenService.hasAnyPermission(item.permissions);
  });

  readonly showProfile = signal(false);
  readonly canViewNotifications = computed(() => this.tokenService.hasPermission('Notification.View'));

  readonly currentUser = this.authService.currentUser;
  readonly companyLogoUrl = this.tenantSettingsStore.logoUrl;
  readonly logoLoadFailed = signal(false);
  readonly logoLoading = signal(false);

  readonly showCompanyLogo = computed(
    () => !!this.companyLogoUrl() && !this.logoLoadFailed(),
  );

  readonly userInitial = computed(
    () => this.currentUser()?.email?.charAt(0)?.toUpperCase() ?? 'U',
  );

  readonly userDisplayName = computed(() => {
    const email = this.currentUser()?.email;
    if (!email) return 'User';
    return email.split('@')[0];
  });

  constructor() {
    effect(() => {
      this.companyLogoUrl();
      this.logoLoadFailed.set(false);
      this.logoLoading.set(!!this.companyLogoUrl());
    });
  }

  ngOnInit(): void {
    this.notificationState.initialize();
  }

  toggleProfile(): void {
    this.showProfile.update((v) => !v);
    this.notificationState.closePanel();
  }

  toggleDarkMode(): void {
    this.themeService.toggleDarkMode();
  }

  closeProfileMenu(): void {
    this.showProfile.set(false);
  }

  onLogoLoad(): void {
    this.logoLoading.set(false);
  }

  onLogoError(): void {
    this.logoLoadFailed.set(true);
    this.logoLoading.set(false);
  }

  logout(): void {
    this.authService.logout();
  }
}
