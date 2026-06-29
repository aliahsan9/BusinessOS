import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { NotificationCenterService } from '../core/services/notification-center.service';
import { NotificationHubService } from '../core/services/notification-hub.service';
import { TokenService } from '../core/services/token.service';
import { NotificationDto } from '../core/models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationStateService {
  private readonly notificationCenter = inject(NotificationCenterService);
  private readonly hub = inject(NotificationHubService);
  private readonly tokenService = inject(TokenService);

  private readonly _items = signal<NotificationDto[]>([]);
  private readonly _unreadCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _panelOpen = signal(false);
  private readonly _connected = signal(false);

  readonly items = this._items.asReadonly();
  readonly unreadCount = this._unreadCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly panelOpen = this._panelOpen.asReadonly();
  readonly connected = this._connected.asReadonly();
  readonly hasUnread = computed(() => this._unreadCount() > 0);

  initialize(): void {
    if (!this.tokenService.hasPermission('Notification.View')) {
      return;
    }

    this.hub.setHandlers({
      onNotification: (notification) => this.prependNotification(notification),
      onUnreadCount: (count) => this._unreadCount.set(count),
    });

    void this.refresh();
    void this.connectHub();
  }

  async refresh(pageSize = 10): Promise<void> {
    if (!this.tokenService.hasPermission('Notification.View')) {
      return;
    }

    this._loading.set(true);
    try {
      const [list, unread] = await Promise.all([
        firstValueFrom(this.notificationCenter.getAll({ page: 1, pageSize })),
        firstValueFrom(this.notificationCenter.getUnreadCount()),
      ]);
      this._items.set(list.items);
      this._unreadCount.set(unread.count);
    } catch {
      this._items.set([]);
      this._unreadCount.set(0);
    } finally {
      this._loading.set(false);
    }
  }

  togglePanel(): void {
    this._panelOpen.update((open) => !open);
    if (this._panelOpen()) {
      void this.refresh();
    }
  }

  closePanel(): void {
    this._panelOpen.set(false);
  }

  async markRead(id: string): Promise<void> {
    await firstValueFrom(this.notificationCenter.markRead(id));
    this._items.update((items) =>
      items.map((item) => (item.id === id ? { ...item, isRead: true } : item)),
    );
    this._unreadCount.update((count) => Math.max(0, count - 1));
  }

  async markAllRead(): Promise<void> {
    await firstValueFrom(this.notificationCenter.markAllRead());
    this._items.update((items) => items.map((item) => ({ ...item, isRead: true })));
    this._unreadCount.set(0);
  }

  async deleteNotification(id: string): Promise<void> {
    const wasUnread = this._items().find((item) => item.id === id && !item.isRead);
    await firstValueFrom(this.notificationCenter.remove(id));
    this._items.update((items) => items.filter((item) => item.id !== id));
    if (wasUnread) {
      this._unreadCount.update((count) => Math.max(0, count - 1));
    }
  }

  private prependNotification(notification: NotificationDto): void {
    this._items.update((items) => {
      const filtered = items.filter((item) => item.id !== notification.id);
      return [notification, ...filtered].slice(0, 10);
    });
    if (!notification.isRead) {
      this._unreadCount.update((count) => count + 1);
    }
  }

  private async connectHub(): Promise<void> {
    try {
      await this.hub.connect();
      this._connected.set(true);
    } catch {
      this._connected.set(false);
    }
  }
}
