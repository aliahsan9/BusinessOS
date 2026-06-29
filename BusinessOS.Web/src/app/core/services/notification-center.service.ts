import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreateNotificationRequest,
  NotificationDto,
  NotificationPreferences,
  NotificationQueryParams,
  UnreadCountResponse,
  UpdateNotificationPreferencesRequest,
} from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationCenterService extends BaseApiService {
  getAll(params?: NotificationQueryParams): Observable<PagedResult<NotificationDto>> {
    return this.get<PagedResult<NotificationDto>>(API_ENDPOINTS.notifications.base, params);
  }

  getUnreadCount(): Observable<UnreadCountResponse> {
    return this.get<UnreadCountResponse>(API_ENDPOINTS.notifications.unreadCount);
  }

  markRead(id: string): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.notifications.base}/read/${id}`, {});
  }

  markAllRead(): Observable<void> {
    return this.put<void>(API_ENDPOINTS.notifications.readAll, {});
  }

  remove(id: string): Observable<void> {
    return this.delete<void>(`${API_ENDPOINTS.notifications.base}/${id}`);
  }

  getPreferences(): Observable<NotificationPreferences> {
    return this.get<NotificationPreferences>(API_ENDPOINTS.notifications.preferences);
  }

  updatePreferences(request: UpdateNotificationPreferencesRequest): Observable<NotificationPreferences> {
    return this.put<NotificationPreferences>(API_ENDPOINTS.notifications.preferences, request);
  }

  create(request: CreateNotificationRequest): Observable<NotificationDto> {
    return this.post<NotificationDto>(API_ENDPOINTS.notifications.base, request);
  }
}
