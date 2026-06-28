import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import {
  BusinessProfileDto,
  TenantSettingsDto,
  UpdateBusinessProfileRequest,
  UpdateTenantSettingsRequest,
} from '../models/settings.model';

@Injectable({ providedIn: 'root' })
export class SettingsService extends BaseApiService {
  getSettings(): Observable<TenantSettingsDto> {
    return this.get<TenantSettingsDto>(API_ENDPOINTS.settings.base);
  }

  updateSettings(request: UpdateTenantSettingsRequest): Observable<TenantSettingsDto> {
    return this.put<TenantSettingsDto>(API_ENDPOINTS.settings.base, request);
  }

  getBusinessProfile(): Observable<BusinessProfileDto> {
    return this.get<BusinessProfileDto>(API_ENDPOINTS.settings.businessProfile);
  }

  updateBusinessProfile(request: UpdateBusinessProfileRequest): Observable<BusinessProfileDto> {
    return this.put<BusinessProfileDto>(API_ENDPOINTS.settings.businessProfile, request);
  }
}
