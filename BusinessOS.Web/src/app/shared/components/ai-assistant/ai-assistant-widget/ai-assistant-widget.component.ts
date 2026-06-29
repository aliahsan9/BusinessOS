import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AiChatWindowComponent } from '../ai-chat-window/ai-chat-window.component';
import { TenantSettingsStoreService } from '../../../../core/services/tenant-settings-store.service';
import { AiAssistantStateService } from '../../../../state/ai-assistant.state';

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
  private readonly aiAssistantState = inject(AiAssistantStateService);
  private readonly router = inject(Router);

  readonly isOpen = this.aiAssistantState.isOpen;

  ngOnInit(): void {
    if (!this.tenantSettingsStore.settings()) {
      this.tenantSettingsStore.load().subscribe();
    }
  }

  toggle(): void {
    this.aiAssistantState.toggle();
  }

  close(): void {
    this.aiAssistantState.close();
  }

  navigate(route: string): void {
    this.close();
    void this.router.navigateByUrl(route);
  }
}
