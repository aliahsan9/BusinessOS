import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PermissionDto } from '../models/role.model';

@Injectable({ providedIn: 'root' })
export class PermissionService extends BaseApiService {
  getAll(): Observable<PermissionDto[]> {
    return this.get<PermissionDto[]>(API_ENDPOINTS.permissions);
  }

  getById(id: string): Observable<PermissionDto> {
    return this.get<PermissionDto>(`${API_ENDPOINTS.permissions}/${id}`);
  }
}
