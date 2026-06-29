import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { DEFAULT_CURRENCY_CODE } from '../constants/currency.constants';
import { TenantSettingsDto } from '../models/settings.model';
import { SettingsService } from './settings.service';

@Injectable({ providedIn: 'root' })
export class TenantSettingsStoreService {
  private readonly settingsService = inject(SettingsService);

  private readonly settingsSignal = signal<TenantSettingsDto | null>(null);

  readonly settings = this.settingsSignal.asReadonly();
  readonly currency = computed(() => this.settingsSignal()?.currency?.trim() || DEFAULT_CURRENCY_CODE);
  readonly logoUrl = computed(() => {
    const url = this.settingsSignal()?.logoUrl?.trim();
    return url || null;
  });
  readonly language = computed(() => this.settingsSignal()?.language?.trim() || 'en');

  load(): Observable<void> {
    return this.settingsService.getSettings().pipe(
      tap((settings) => this.applySettings(settings)),
      map(() => undefined),
      catchError(() => of(undefined)),
    );
  }

  applySettings(settings: TenantSettingsDto): void {
    this.settingsSignal.set(settings);
  }

  clear(): void {
    this.settingsSignal.set(null);
  }
}
