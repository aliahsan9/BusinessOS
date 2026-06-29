import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import {
  RegisterBusinessRequest,
  TenantDashboardDto,
  TenantDto,
  TenantSettingsDto,
  TenantUsageDto,
  UpdateTenantRequest,
  UpdateTenantSettingsRequest,
} from '../models/tenant.model';
import { AuthResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class TenantService extends BaseApiService {
  getTenant(): Observable<TenantDto> {
    return this.get<TenantDto>('/tenant');
  }

  updateTenant(request: UpdateTenantRequest): Observable<TenantDto> {
    return this.put<TenantDto>('/tenant', request);
  }

  getUsage(): Observable<TenantUsageDto> {
    return this.get<TenantUsageDto>('/tenant/usage');
  }

  getDashboard(): Observable<TenantDashboardDto> {
    return this.get<TenantDashboardDto>('/tenant/dashboard');
  }

  getSettings(): Observable<TenantSettingsDto> {
    return this.get<TenantSettingsDto>('/tenant/settings');
  }

  updateSettings(request: UpdateTenantSettingsRequest): Observable<TenantSettingsDto> {
    return this.put<TenantSettingsDto>('/tenant/settings', request);
  }

  registerBusiness(request: RegisterBusinessRequest): Observable<AuthResponse> {
    return this.post<AuthResponse>('/register-business', request);
  }
}
