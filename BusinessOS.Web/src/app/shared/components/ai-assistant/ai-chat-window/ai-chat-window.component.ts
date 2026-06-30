import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  computed,
  output,
  ElementRef,
  viewChild,
  afterNextRender,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AiChatService } from '../../../../core/services/ai-chat.service';
import { AiContextService } from '../../../../core/services/ai-context.service';
import { AiRetrievalService } from '../../../../core/services/ai-retrieval.service';
import { AiActionService } from '../../../../core/services/ai-action.service';
import { AiPromptBuilderService } from '../../../../core/services/ai-prompt-builder.service';
import {
  AiChatMessage,
  AiChatResponse,
  AiQuickActionDto,
  AiSearchResultDto,
  AiStreamChunk,
  AiSuggestionDto,
} from '../../../../core/models/ai.model';
import { ApiError } from '../../../../core/models/api-error.model';
import { AiAssistantStateService } from '../../../../state/ai-assistant.state';
import { ROUTES } from '../../../../core/constants/route.constants';
import { TenantSettingsStoreService } from '../../../../core/services/tenant-settings-store.service';

@Component({
  selector: 'app-ai-chat-window',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './ai-chat-window.component.html',
  styleUrl: './ai-chat-window.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiChatWindowComponent {
  private readonly aiChatService = inject(AiChatService);
  private readonly aiContextService = inject(AiContextService);
  private readonly aiRetrievalService = inject(AiRetrievalService);
  private readonly aiActionService = inject(AiActionService);
  private readonly aiPromptBuilder = inject(AiPromptBuilderService);
  private readonly aiAssistantState = inject(AiAssistantStateService);
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);
  private readonly messagesContainer = viewChild<ElementRef<HTMLDivElement>>('messagesContainer');

  readonly close = output<void>();
  readonly navigate = output<string>();

  readonly messages = signal<AiChatMessage[]>([
    {
      role: 'assistant',
      content:
        "Hi! I'm BusinessOS AI Copilot. Ask about revenue, invoices, projects, customers, or say \"What should I focus on today?\" — I'll answer using your live business data.",
      timestamp: new Date(),
    },
  ]);
  readonly suggestions = signal<AiSuggestionDto[]>([]);
  readonly quickActions = signal<AiQuickActionDto[]>([]);
  readonly searchResults = signal<AiSearchResultDto[]>([]);
  readonly inputText = signal('');
  readonly searchText = signal('');
  readonly loading = signal(false);
  readonly streamingEnabled = signal(true);
  readonly showSuggestions = computed(
    () => this.tenantSettingsStore.settings()?.aiShowSuggestions ?? true,
  );
  readonly chatEnabled = this.aiAssistantState.chatEnabled;
  readonly settingsRoute = ROUTES.settings.hub;
  readonly workspaceRoute = ROUTES.ai.workspace;
  readonly contextLabel = computed(() =>
    this.aiPromptBuilder.buildContextLabel(this.aiContextService.buildPageContext()),
  );

  constructor() {
    afterNextRender(() => this.scrollToBottom());
  }

  sendMessage(text?: string): void {
    const message = (text ?? this.inputText()).trim();
    if (!message || this.loading()) return;

    if (!this.chatEnabled()) {
      this.appendMessage(
        'assistant',
        'The AI assistant is turned off in tenant settings. Enable it under Settings to start chatting.',
      );
      return;
    }

    this.inputText.set('');
    this.appendMessage('user', message);
    this.loading.set(true);

    if (this.streamingEnabled()) {
      void this.sendStreaming(message);
      return;
    }

    this.aiChatService.chat(message).subscribe({
      next: (response) => this.handleResponse(response),
      error: (err: ApiError) => this.handleError(err),
    });
  }

  private async sendStreaming(message: string): Promise<void> {
    const streamMsg: AiChatMessage = {
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      streaming: true,
    };
    this.messages.update((msgs) => [...msgs, streamMsg]);
    const msgIndex = this.messages().length - 1;

    try {
      for await (const chunk of this.aiChatService.streamMessage(message)) {
        if (chunk.type === 'token' && chunk.content) {
          this.messages.update((msgs) => {
            const copy = [...msgs];
            copy[msgIndex] = { ...copy[msgIndex], content: copy[msgIndex].content + chunk.content };
            return copy;
          });
          this.scrollToBottom();
        }

        if (chunk.type === 'done' && chunk.finalResponse) {
          this.applyFinalResponse(chunk.finalResponse, msgIndex);
        }
      }
    } catch {
      this.messages.update((msgs) => {
        const copy = [...msgs];
        copy[msgIndex] = {
          ...copy[msgIndex],
          content: 'Sorry, streaming failed. Please try again.',
          streaming: false,
        };
        return copy;
      });
    } finally {
      this.loading.set(false);
    }
  }

  private handleResponse(response: AiChatResponse): void {
    if (response.sessionId) {
      this.aiChatService.setSessionId(response.sessionId);
    }
    this.appendMessage(
      'assistant',
      response.reply,
      response.sources,
      response.actionResult,
      response.citations,
      response.toolsUsed,
    );
    if (this.showSuggestions()) {
      this.suggestions.set(response.suggestions);
    }
    this.quickActions.set(response.quickActions);
    this.searchResults.set(response.searchResults);

    const navRoute = response.actionResult
      ? this.aiActionService.shouldNavigate(response.actionResult)
      : null;
    if (navRoute) {
      this.navigate.emit(navRoute);
    }

    this.loading.set(false);
  }

  private applyFinalResponse(response: AiChatResponse, msgIndex: number): void {
    if (response.sessionId) {
      this.aiChatService.setSessionId(response.sessionId);
    }
    this.messages.update((msgs) => {
      const copy = [...msgs];
      copy[msgIndex] = {
        ...copy[msgIndex],
        content: response.reply,
        streaming: false,
        sources: response.sources,
        citations: response.citations,
        toolsUsed: response.toolsUsed,
        actionResult: response.actionResult,
      };
      return copy;
    });
    if (this.showSuggestions()) {
      this.suggestions.set(response.suggestions);
    }
    this.quickActions.set(response.quickActions);
    this.searchResults.set(response.searchResults);
  }

  runSearch(): void {
    const query = this.searchText().trim();
    if (!query || this.loading()) return;

    if (!this.chatEnabled()) {
      this.appendMessage(
        'assistant',
        'Search is unavailable while the AI assistant is disabled in tenant settings.',
      );
      return;
    }

    this.loading.set(true);
    this.aiChatService.search(query).subscribe({
      next: (response) => {
        this.searchResults.set(response.searchResults);
        this.appendMessage(
          'assistant',
          response.searchResults.length
            ? `Found ${response.searchResults.length} result(s) for "${query}".`
            : `No results found for "${query}".`,
          response.sources,
        );
        this.loading.set(false);
      },
      error: (err: ApiError) => this.handleError(err),
    });
  }

  sourcesSummary(msg: AiChatMessage): string | null {
    if (!msg.sources || !this.aiRetrievalService.hasRetrievedData(msg.sources)) return null;
    return this.aiRetrievalService.formatSourcesSummary(msg.sources);
  }

  useSuggestion(suggestion: AiSuggestionDto): void {
    this.sendMessage(suggestion.message);
  }

  goTo(route: string): void {
    this.navigate.emit(route);
  }

  onClose(): void {
    this.close.emit();
  }

  private appendMessage(
    role: 'user' | 'assistant',
    content: string,
    sources?: AiChatMessage['sources'],
    actionResult?: AiChatMessage['actionResult'],
    citations?: AiChatMessage['citations'],
    toolsUsed?: AiChatMessage['toolsUsed'],
  ): void {
    this.messages.update((msgs) => [
      ...msgs,
      { role, content, timestamp: new Date(), sources, actionResult, citations, toolsUsed },
    ]);
    setTimeout(() => this.scrollToBottom(), 50);
  }

  private handleError(err: ApiError): void {
    this.appendMessage('assistant', this.formatErrorMessage(err));
    this.loading.set(false);
  }

  private scrollToBottom(): void {
    const el = this.messagesContainer()?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }

  private formatErrorMessage(err: ApiError): string {
    const message = err.detail?.trim() || err.title?.trim();
    if (message) {
      return message;
    }

    return 'Sorry, I could not reach the AI service. Please try again in a moment.';
  }
}
