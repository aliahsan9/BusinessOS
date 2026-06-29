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
import { Router, RouterLink } from '@angular/router';
import { AiService } from '../../../../core/services/ai.service';
import { AiChatMessage, AiQuickActionDto, AiSearchResultDto, AiSuggestionDto } from '../../../../core/models/ai.model';
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
  private readonly aiService = inject(AiService);
  private readonly router = inject(Router);
  private readonly aiAssistantState = inject(AiAssistantStateService);
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);
  private readonly messagesContainer = viewChild<ElementRef<HTMLDivElement>>('messagesContainer');

  readonly close = output<void>();
  readonly navigate = output<string>();

  readonly messages = signal<AiChatMessage[]>([
    {
      role: 'assistant',
      content: "Hi! I'm BusinessOS AI. Ask me anything about customers, projects, invoices, expenses, analytics, or reports.",
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

    const currentPage = this.router.url;

    this.aiService.chat({ message, currentPage }).subscribe({
      next: (response) => {
        this.appendMessage('assistant', response.reply);
        if (this.showSuggestions()) {
          this.suggestions.set(response.suggestions);
        }
        this.quickActions.set(response.quickActions);
        this.searchResults.set(response.searchResults);
        this.loading.set(false);
      },
      error: () => {
        this.appendMessage(
          'assistant',
          'Sorry, I encountered an error. Your plan may not include AI Assistant, or the service is temporarily unavailable.',
        );
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
    this.aiService.chat({ message: '', searchQuery: query, currentPage: this.router.url }).subscribe({
      next: (response) => {
        this.searchResults.set(response.searchResults);
        this.appendMessage(
          'assistant',
          response.searchResults.length
            ? `Found ${response.searchResults.length} result(s) for "${query}".`
            : `No results found for "${query}".`,
        );
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
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

  private appendMessage(role: 'user' | 'assistant', content: string): void {
    this.messages.update((msgs) => [...msgs, { role, content, timestamp: new Date() }]);
    setTimeout(() => this.scrollToBottom(), 50);
  }

  private scrollToBottom(): void {
    const el = this.messagesContainer()?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }
}
