import { Injectable, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { TokenService } from './token.service';
import { NotificationDto } from '../models/notification.model';
import { ActivityDto } from '../models/activity.model';

export interface RealtimeHandlers {
  onNotification?: (notification: NotificationDto) => void;
  onActivity?: (activity: ActivityDto) => void;
  onUnreadCount?: (count: number) => void;
}

@Injectable({ providedIn: 'root' })
export class NotificationHubService {
  private readonly tokenService = inject(TokenService);
  private connection: signalR.HubConnection | null = null;
  private handlers: RealtimeHandlers = {};

  setHandlers(handlers: RealtimeHandlers): void {
    this.handlers = handlers;
  }

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (!this.tokenService.isAuthenticated()) {
      return;
    }

    await this.disconnect();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        accessTokenFactory: () => this.tokenService.getToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('ReceiveNotification', (notification: NotificationDto) => {
      this.handlers.onNotification?.(notification);
    });

    this.connection.on('ReceiveActivity', (activity: ActivityDto) => {
      this.handlers.onActivity?.(activity);
    });

    this.connection.on('UnreadCountUpdated', (count: number) => {
      this.handlers.onUnreadCount?.(count);
    });

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }
}
