import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import {
  AssignPermissionRequest,
  CreateRoleRequest,
  RoleDto,
  UpdateRoleRequest,
} from '../models/role.model';

@Injectable({ providedIn: 'root' })
export class RoleService extends BaseApiService {
  getAll(): Observable<RoleDto[]> {
    return this.get<RoleDto[]>(API_ENDPOINTS.roles);
  }

  getById(id: string): Observable<RoleDto> {
    return this.get<RoleDto>(`${API_ENDPOINTS.roles}/${id}`);
  }

  create(request: CreateRoleRequest): Observable<RoleDto> {
    return this.post<RoleDto>(API_ENDPOINTS.roles, request);
  }

  update(id: string, request: UpdateRoleRequest): Observable<RoleDto> {
    return this.put<RoleDto>(`${API_ENDPOINTS.roles}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.roles}/${id}`);
  }

  assignPermission(roleId: string, request: AssignPermissionRequest): Observable<void> {
    return this.post<void>(`${API_ENDPOINTS.roles}/${roleId}/permissions`, request);
  }

  removePermission(roleId: string, permissionId: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.roles}/${roleId}/permissions/${permissionId}`);
  }
}
