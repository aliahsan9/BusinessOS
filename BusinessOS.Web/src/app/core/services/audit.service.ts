import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import { AuditLogDto, AuditLogQueryParams } from '../models/audit.model';

@Injectable({ providedIn: 'root' })
export class AuditService extends BaseApiService {
  getAll(params?: AuditLogQueryParams): Observable<PagedResult<AuditLogDto>> {
    return this.get<PagedResult<AuditLogDto>>(API_ENDPOINTS.auditLogs, params);
  }
}
