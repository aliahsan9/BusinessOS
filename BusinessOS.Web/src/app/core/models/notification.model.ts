import { PaginationParams } from './pagination.model';

export interface NotificationDto {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationPreferences {
  emailNotificationsEnabled: boolean;
  systemNotificationsEnabled: boolean;
  orderNotificationsEnabled: boolean;
  inventoryAlertsEnabled: boolean;
  paymentAlertsEnabled: boolean;
}

export type UpdateNotificationPreferencesRequest = NotificationPreferences;

export interface CreateNotificationRequest {
  userId: string;
  title: string;
  message: string;
  type: string;
}

export interface NotificationQueryParams extends PaginationParams {
  unreadOnly?: boolean;
}
