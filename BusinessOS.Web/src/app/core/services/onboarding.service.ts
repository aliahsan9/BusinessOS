import { Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import {
  OnboardingBusinessProfileDto,
  OnboardingStatusDto,
  SaveOnboardingBusinessProfileRequest,
  SaveOnboardingProgressRequest,
} from '../models/onboarding.model';

@Injectable({ providedIn: 'root' })
export class OnboardingService extends BaseApiService {
  private readonly _status = signal<OnboardingStatusDto | null>(null);

  readonly status = this._status.asReadonly();

  getStatus(): Observable<OnboardingStatusDto> {
    return this.get<OnboardingStatusDto>(API_ENDPOINTS.onboarding.status).pipe(
      tap((status) => this._status.set(status)),
    );
  }

  saveProgress(request: SaveOnboardingProgressRequest): Observable<OnboardingStatusDto> {
    return this.post<OnboardingStatusDto>(API_ENDPOINTS.onboarding.saveProgress, request).pipe(
      tap((status) => this._status.set(status)),
    );
  }

  complete(): Observable<OnboardingStatusDto> {
    return this.post<OnboardingStatusDto>(API_ENDPOINTS.onboarding.complete, {}).pipe(
      tap((status) => this._status.set(status)),
    );
  }

  getBusinessProfile(): Observable<OnboardingBusinessProfileDto> {
    return this.get<OnboardingBusinessProfileDto>(API_ENDPOINTS.onboarding.businessProfile);
  }

  saveBusinessProfile(request: SaveOnboardingBusinessProfileRequest): Observable<void> {
    return this.put<void>(API_ENDPOINTS.onboarding.businessProfile, request);
  }

  shouldShowWizard(): boolean {
    return this._status()?.shouldShowWizard ?? false;
  }
}
