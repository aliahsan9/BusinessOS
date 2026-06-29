import { ChangeDetectionStrategy, Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AiChatWindowComponent } from '../ai-chat-window/ai-chat-window.component';
import { TenantSettingsStoreService } from '../../../../core/services/tenant-settings-store.service';

@Component({
  selector: 'app-ai-assistant-widget',
  standalone: true,
  imports: [AiChatWindowComponent],
  templateUrl: './ai-assistant-widget.component.html',
  styleUrl: './ai-assistant-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiAssistantWidgetComponent implements OnInit {
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);
  private readonly router = inject(Router);

  readonly isOpen = signal(false);

  readonly isEnabled = computed(
    () => this.tenantSettingsStore.settings()?.aiAssistantEnabled ?? true,
  );

  ngOnInit(): void {
    if (!this.tenantSettingsStore.settings()) {
      this.tenantSettingsStore.load().subscribe();
    }
  }

  toggle(): void {
    this.isOpen.update((v) => !v);
  }

  close(): void {
    this.isOpen.set(false);
  }

  navigate(route: string): void {
    this.close();
    void this.router.navigateByUrl(route);
  }
}
