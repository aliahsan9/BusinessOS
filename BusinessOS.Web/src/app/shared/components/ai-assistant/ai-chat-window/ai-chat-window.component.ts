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
import { AiChatMessage, AiQuickActionDto, AiSearchResultDto, AiSuggestionDto } from '../../../../core/models/ai.model';
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
        "Hi! I'm BusinessOS AI. Ask about your customers, projects, invoices, and analytics — I'll answer using your real business data.",
      timestamp: new Date(),
    },
  ]);
  readonly suggestions = signal<AiSuggestionDto[]>([]);
  readonly quickActions = signal<AiQuickActionDto[]>([]);
  readonly searchResults = signal<AiSearchResultDto[]>([]);
  readonly inputText = signal('');
  readonly searchText = signal('');
  readonly loading = signal(false);
  readonly showSuggestions = computed(
    () => this.tenantSettingsStore.settings()?.aiShowSuggestions ?? true,
  );
  readonly chatEnabled = this.aiAssistantState.chatEnabled;
  readonly settingsRoute = ROUTES.settings.hub;
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
        'The AI assistant is turned off in your tenant settings. Enable it under Settings to start chatting.',
      );
      return;
    }

    this.inputText.set('');
    this.appendMessage('user', message);
    this.loading.set(true);

    this.aiChatService.chat(message).subscribe({
      next: (response) => {
        this.appendMessage('assistant', response.reply, response.sources, response.actionResult);
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
      },
      error: (err: ApiError) => {
        this.appendMessage('assistant', this.formatErrorMessage(err));
        this.loading.set(false);
      },
    });
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
      error: (err: ApiError) => {
        this.appendMessage('assistant', this.formatErrorMessage(err));
        this.loading.set(false);
      },
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
  ): void {
    this.messages.update((msgs) => [
      ...msgs,
      { role, content, timestamp: new Date(), sources, actionResult },
    ]);
    setTimeout(() => this.scrollToBottom(), 50);
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
