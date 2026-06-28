import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  AssignUserRoleRequest,
  CreateUserRequest,
  ResetPasswordRequest,
  UpdateUserRequest,
  UserDto,
  UserQueryParams,
  UserSummaryDto,
} from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserService extends BaseApiService {
  getAll(params?: UserQueryParams): Observable<PagedResult<UserSummaryDto>> {
    return this.get<PagedResult<UserSummaryDto>>(API_ENDPOINTS.users, params);
  }

  getById(id: string): Observable<UserDto> {
    return this.get<UserDto>(`${API_ENDPOINTS.users}/${id}`);
  }

  create(request: CreateUserRequest): Observable<UserDto> {
    return this.post<UserDto>(API_ENDPOINTS.users, request);
  }

  update(id: string, request: UpdateUserRequest): Observable<UserDto> {
    return this.put<UserDto>(`${API_ENDPOINTS.users}/${id}`, request);
  }

  activate(id: string): Observable<void> {
    return this.post<void>(`${API_ENDPOINTS.users}/${id}/activate`, {});
  }

  deactivate(id: string): Observable<void> {
    return this.post<void>(`${API_ENDPOINTS.users}/${id}/deactivate`, {});
  }

  resetPassword(id: string, request: ResetPasswordRequest): Observable<void> {
    return this.post<void>(`${API_ENDPOINTS.users}/${id}/reset-password`, request);
  }

  assignRole(userId: string, request: AssignUserRoleRequest): Observable<void> {
    return this.post<void>(`${API_ENDPOINTS.users}/${userId}/roles`, request);
  }

  removeRole(userId: string, roleId: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.users}/${userId}/roles/${roleId}`);
  }
}
