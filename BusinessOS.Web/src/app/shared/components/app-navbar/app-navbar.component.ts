import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { APP_ROUTE_PATHS } from '../../constants/nav.constants';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './app-navbar.component.html',
  styleUrl: './app-navbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppNavbarComponent {
  private readonly authService = inject(AuthService);

  readonly menuToggle = output<void>();
  readonly routes = APP_ROUTE_PATHS;

  readonly searchQuery = signal('');
  readonly showNotifications = signal(false);
  readonly showProfile = signal(false);

  readonly currentUser = this.authService.currentUser;

  readonly notifications = signal([
    { id: '1', title: 'Low stock alert', message: '3 products need reordering', read: false },
    { id: '2', title: 'New order', message: 'Order #1042 received', read: false },
    { id: '3', title: 'Welcome', message: 'Your dashboard is ready', read: true },
  ]);

  readonly hasUnreadNotifications = computed(() => this.notifications().some((n) => !n.read));

  onSearch(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchQuery.set(value);
  }

  toggleNotifications(): void {
    this.showNotifications.update((v) => !v);
    this.showProfile.set(false);
  }

  toggleProfile(): void {
    this.showProfile.update((v) => !v);
    this.showNotifications.set(false);
  }

  closeProfileMenu(): void {
    this.showProfile.set(false);
  }

  logout(): void {
    this.authService.logout();
  }
}
