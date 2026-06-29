import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  InviteTeamMemberRequest,
  TeamActivityDto,
  TeamDashboardDto,
  TeamInvitationDto,
  TeamMemberDto,
  UpdateTeamMemberRequest,
} from '../models/team.model';

@Injectable({ providedIn: 'root' })
export class TeamService extends BaseApiService {
  getDashboard(): Observable<TeamDashboardDto> {
    return this.get<TeamDashboardDto>(API_ENDPOINTS.team.base);
  }

  getMembers(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    role?: string;
  }): Observable<PagedResult<TeamMemberDto>> {
    return this.get<PagedResult<TeamMemberDto>>(API_ENDPOINTS.team.base + '/members', params);
  }

  getMember(id: string): Observable<TeamMemberDto> {
    return this.get<TeamMemberDto>(`${API_ENDPOINTS.team.base}/members/${id}`);
  }

  invite(request: InviteTeamMemberRequest): Observable<TeamInvitationDto> {
    return this.post<TeamInvitationDto>(API_ENDPOINTS.team.invite, request);
  }

  updateMember(id: string, request: UpdateTeamMemberRequest): Observable<TeamMemberDto> {
    return this.put<TeamMemberDto>(`${API_ENDPOINTS.team.member}/${id}`, request);
  }

  removeMember(id: string): Observable<void> {
    return this.delete<void>(`${API_ENDPOINTS.team.member}/${id}`);
  }

  getActivity(limit = 20): Observable<TeamActivityDto[]> {
    return this.get<TeamActivityDto[]>(API_ENDPOINTS.team.activity, { limit });
  }

  getPendingInvitations(): Observable<TeamInvitationDto[]> {
    return this.get<TeamInvitationDto[]>(API_ENDPOINTS.team.invitations);
  }
}
