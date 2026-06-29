import { Injectable, computed, inject } from '@angular/core';
import { DEFAULT_CURRENCY_CODE } from '../constants/currency.constants';
import { TenantSettingsStoreService } from './tenant-settings-store.service';

@Injectable({ providedIn: 'root' })
export class CurrencyService {
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);

  readonly currencyCode = computed(
    () => this.tenantSettingsStore.currency() || DEFAULT_CURRENCY_CODE,
  );
  readonly locale = computed(() => this.tenantSettingsStore.language() || 'en');
}
