import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import { ActivityDto, ActivityQueryParams } from '../models/activity.model';

@Injectable({ providedIn: 'root' })
export class ActivityService extends BaseApiService {
  getAll(params?: ActivityQueryParams): Observable<PagedResult<ActivityDto>> {
    return this.get<PagedResult<ActivityDto>>(API_ENDPOINTS.activity.base, params);
  }

  getRecent(limit = 10): Observable<ActivityDto[]> {
    return this.get<ActivityDto[]>(API_ENDPOINTS.activity.recent, { limit });
  }
}
