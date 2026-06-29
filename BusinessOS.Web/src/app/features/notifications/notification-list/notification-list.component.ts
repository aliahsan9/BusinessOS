import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { NotificationService } from '../../../core/services/notification.service';
import { NotificationDto } from '../../../core/models/notification.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-notification-list',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
  ],
  templateUrl: './notification-list.component.html',
  styleUrl: './notification-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly notificationCenter = inject(NotificationCenterService);
  private readonly notification = inject(NotificationService);

  readonly items = signal<NotificationDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly unreadOnly = signal(false);
  readonly routes = ROUTES;
  readonly breadcrumbs = [{ label: 'Notifications', route: ROUTES.notifications.list }];
  readonly headerActions = [{ label: 'Settings', route: ROUTES.notifications.settings, icon: '⚙️' }];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.notificationCenter
      .getAll({ page, pageSize: this.pageSize(), unreadOnly: this.unreadOnly() || undefined })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load notifications.');
          this.loading.set(false);
        },
      });
  }

  onFilterChange(): void {
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  markRead(item: NotificationDto): void {
    if (item.isRead) return;
    this.notificationCenter.markRead(item.id).subscribe({
      next: () => {
        this.notification.success('Marked as read.');
        this.load();
      },
      error: () => this.notification.error('Failed to mark as read.'),
    });
  }

  markAllRead(): void {
    this.notificationCenter.markAllRead().subscribe({
      next: () => {
        this.notification.success('All notifications marked as read.');
        this.load();
      },
      error: () => this.notification.error('Failed to mark all as read.'),
    });
  }

  deleteItem(item: NotificationDto): void {
    this.notificationCenter.remove(item.id).subscribe({
      next: () => {
        this.notification.success('Notification deleted.');
        this.load();
      },
      error: () => this.notification.error('Failed to delete notification.'),
    });
  }

  retry(): void {
    this.load();
  }
}
