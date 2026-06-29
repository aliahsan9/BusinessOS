import { PaginationParams } from './pagination.model';

export interface NotificationDto {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  link?: string | null;
  createdAt: string;
  createdBy?: string | null;
}

export interface NotificationPreferences {
  emailNotificationsEnabled: boolean;
  systemNotificationsEnabled: boolean;
  orderNotificationsEnabled: boolean;
  inventoryAlertsEnabled: boolean;
  paymentAlertsEnabled: boolean;
  taskNotificationsEnabled: boolean;
  invoiceNotificationsEnabled: boolean;
  customerNotificationsEnabled: boolean;
  projectNotificationsEnabled: boolean;
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

export interface UnreadCountResponse {
  count: number;
}

export const NOTIFICATION_TYPE_VARIANTS: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
  Success: 'success',
  Info: 'info',
  Warning: 'warning',
  Error: 'danger',
  System: 'neutral',
  Business: 'primary',
  Billing: 'primary',
  Task: 'info',
  Project: 'primary',
  Customer: 'info',
};
