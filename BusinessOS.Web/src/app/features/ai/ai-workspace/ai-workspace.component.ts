import { ChangeDetectionStrategy, Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AiChatService } from '../../../core/services/ai-chat.service';
import { AiContextService } from '../../../core/services/ai-context.service';
import {
  AiChatMessage,
  AiChatResponse,
  AiConversationSession,
  AiSuggestionDto,
} from '../../../core/models/ai.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';

@Component({
  selector: 'app-ai-workspace',
  standalone: true,
  imports: [FormsModule, RouterLink, AppBreadcrumbComponent, AppCardComponent],
  templateUrl: './ai-workspace.component.html',
  styleUrl: './ai-workspace.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiWorkspaceComponent implements OnInit {
  private readonly aiChat = inject(AiChatService);
  private readonly aiContext = inject(AiContextService);

  readonly routes = ROUTES;
  readonly messages = signal<AiChatMessage[]>([]);
  readonly sessions = signal<AiConversationSession[]>([]);
  readonly suggestions = signal<AiSuggestionDto[]>([
    { label: 'Revenue this month', message: 'What is our revenue this month?' },
    { label: 'Products sold', message: 'How many products sold this month?' },
    { label: 'Overdue invoices', message: 'Show overdue invoices' },
    { label: 'Top customers', message: 'Who are the top customers by revenue?' },
    { label: 'Focus today', message: 'What should I focus on today?' },
  ]);
  readonly input = signal('');
  readonly loading = signal(false);
  readonly breadcrumbs = [
    { label: 'AI Copilot', route: ROUTES.ai.workspace },
    { label: 'Workspace' },
  ];

  ngOnInit(): void {
    this.aiChat.listConversations(30).subscribe({
      next: (sessions) => this.sessions.set(sessions),
      error: () => this.sessions.set([]),
    });
  }

  send(text?: string): void {
    const message = (text ?? this.input()).trim();
    if (!message || this.loading()) return;
    this.input.set('');
    this.push('user', message);
    this.loading.set(true);

    this.aiChat.chat(message).subscribe({
      next: (response) => this.applyResponse(response),
      error: () => {
        this.push('assistant', 'Unable to reach AI Copilot. Please try again.');
        this.loading.set(false);
      },
    });
  }

  loadSession(sessionId: string): void {
    this.aiChat.setSessionId(sessionId);
    this.messages.set([]);
    this.aiChat.getConversation(sessionId).subscribe({
      next: (items) => {
        const msgs: AiChatMessage[] = [];
        for (const item of items) {
          msgs.push({ role: 'user', content: item.prompt, timestamp: new Date(item.createdAt) });
          msgs.push({
            role: 'assistant',
            content: item.response,
            timestamp: new Date(item.createdAt),
            citations: item.citations,
            toolsUsed: item.toolsUsed,
          });
        }
        this.messages.set(msgs);
      },
    });
  }

  private applyResponse(response: AiChatResponse): void {
    if (response.sessionId) this.aiChat.setSessionId(response.sessionId);
    this.push('assistant', response.reply, response.citations, response.toolsUsed);
    this.suggestions.set(response.suggestions.length ? response.suggestions : this.suggestions());
    this.loading.set(false);
    this.ngOnInit();
  }

  private push(
    role: 'user' | 'assistant',
    content: string,
    citations?: AiChatMessage['citations'],
    toolsUsed?: AiChatMessage['toolsUsed'],
  ): void {
    this.messages.update((m) => [...m, { role, content, timestamp: new Date(), citations, toolsUsed }]);
  }
}
