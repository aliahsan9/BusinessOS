import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { EnvironmentInfo, SystemHealth, SystemStats } from '../models/system.model';

@Injectable({ providedIn: 'root' })
export class SystemAdminService extends BaseApiService {
  getHealth(): Observable<SystemHealth> {
    return this.get<SystemHealth>(API_ENDPOINTS.system.health);
  }

  getStats(): Observable<SystemStats> {
    return this.get<SystemStats>(API_ENDPOINTS.system.stats);
  }

  getEnvironment(): Observable<EnvironmentInfo> {
    return this.get<EnvironmentInfo>(API_ENDPOINTS.system.environment);
  }
}
