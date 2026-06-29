import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, output, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { TokenService } from '../../../core/services/token.service';
import { TenantSettingsStoreService } from '../../../core/services/tenant-settings-store.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { ThemeService } from '../../../core/theme/theme.service';
import { APP_ROUTE_PATHS, TOP_NAV_ITEMS } from '../../constants/nav.constants';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-navbar.component.html',
  styleUrl: './app-navbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppNavbarComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly tokenService = inject(TokenService);
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);
  private readonly notificationCenter = inject(NotificationCenterService);
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

  readonly showNotifications = signal(false);
  readonly showProfile = signal(false);

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

  constructor() {
    effect(() => {
      this.companyLogoUrl();
      this.logoLoadFailed.set(false);
      this.logoLoading.set(!!this.companyLogoUrl());
    });
  }

  readonly notifications = signal<{ id: string; title: string; message: string; read: boolean }[]>([]);

  readonly hasUnreadNotifications = computed(() => this.notifications().some((n) => !n.read));

  readonly userDisplayName = computed(() => {
    const email = this.currentUser()?.email;
    if (!email) return 'User';
    return email.split('@')[0];
  });

  ngOnInit(): void {
    if (this.tokenService.hasPermission('Notification.View')) {
      this.notificationCenter.getAll({ page: 1, pageSize: 5 }).subscribe({
        next: (result) =>
          this.notifications.set(
            result.items.map((n) => ({
              id: n.id,
              title: n.title,
              message: n.message,
              read: n.isRead,
            })),
          ),
        error: () => {
          // Keep navbar usable when notifications cannot be loaded.
          this.notifications.set([]);
        },
      });
    }
  }

  toggleNotifications(): void {
    this.showNotifications.update((v) => !v);
    this.showProfile.set(false);
  }

  toggleProfile(): void {
    this.showProfile.update((v) => !v);
    this.showNotifications.set(false);
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
