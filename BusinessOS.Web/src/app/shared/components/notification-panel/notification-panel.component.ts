import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { NotificationStateService } from '../../../state/notification.state';
import { NotificationService } from '../../../core/services/notification.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { NOTIFICATION_TYPE_VARIANTS } from '../../../core/models/notification.model';
import { AppBadgeComponent } from '../app-badge/app-badge.component';
import { AppSkeletonComponent } from '../app-skeleton/app-skeleton.component';

@Component({
  selector: 'app-notification-panel',
  standalone: true,
  imports: [RouterLink, DatePipe, AppBadgeComponent, AppSkeletonComponent],
  templateUrl: './notification-panel.component.html',
  styleUrl: './notification-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationPanelComponent {
  private readonly toast = inject(NotificationService);
  private readonly router = inject(Router);
  readonly notificationState = inject(NotificationStateService);
  readonly routes = ROUTES;
  readonly typeVariants = NOTIFICATION_TYPE_VARIANTS;

  markRead(id: string, event: Event): void {
    event.stopPropagation();
    void this.notificationState.markRead(id).catch(() => this.toast.error('Failed to mark as read.'));
  }

  markAllRead(): void {
    void this.notificationState.markAllRead().catch(() => this.toast.error('Failed to mark all as read.'));
  }

  deleteNotification(id: string, event: Event): void {
    event.stopPropagation();
    void this.notificationState.deleteNotification(id).catch(() => this.toast.error('Failed to delete notification.'));
  }

  getTypeVariant(type: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    return this.typeVariants[type] ?? 'neutral';
  }

  openNotification(link: string | null | undefined, isRead: boolean, id: string): void {
    if (!isRead) {
      void this.notificationState.markRead(id);
    }

    if (link) {
      this.notificationState.closePanel();
      void this.router.navigateByUrl(link);
    }
  }
}
