import { Injectable, computed, inject, signal } from '@angular/core';
import { TenantSettingsStoreService } from '../core/services/tenant-settings-store.service';

@Injectable({ providedIn: 'root' })
export class AiAssistantStateService {
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);

  private readonly _isOpen = signal(false);

  readonly isOpen = this._isOpen.asReadonly();
  readonly chatEnabled = computed(
    () => this.tenantSettingsStore.settings()?.aiAssistantEnabled ?? true,
  );

  open(): void {
    this._isOpen.set(true);
  }

  close(): void {
    this._isOpen.set(false);
  }

  toggle(): void {
    this._isOpen.update((open) => !open);
  }
}
