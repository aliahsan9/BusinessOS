import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { OrganizationDto, UpdateOrganizationRequest } from '../models/team.model';

@Injectable({ providedIn: 'root' })
export class OrganizationService extends BaseApiService {
  getOrganization(): Observable<OrganizationDto> {
    return this.get<OrganizationDto>(API_ENDPOINTS.organization);
  }

  update(request: UpdateOrganizationRequest): Observable<OrganizationDto> {
    return this.put<OrganizationDto>(API_ENDPOINTS.organization, request);
  }
}
