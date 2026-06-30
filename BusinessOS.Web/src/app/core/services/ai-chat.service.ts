import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS, HTTP_HEADERS } from '../constants/api.constants';
import {
  AiChatResponse,
  AiConversationMessage,
  AiConversationSession,
  AiCopilotChatRequest,
  AiDashboardCopilot,
  AiDiagnosticsSummary,
  AiProactiveInsight,
  AiStreamChunk,
} from '../models/ai.model';
import { AiContextService } from './ai-context.service';
import { TokenService } from './token.service';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AiChatService extends BaseApiService {
  private readonly aiContext = inject(AiContextService);
  private readonly tokenService = inject(TokenService);

  private sessionId: string | null = null;

  chat(message: string): Observable<AiChatResponse> {
    const request = this.buildCopilotRequest(message, false);
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }

  streamMessage(message: string): AsyncGenerator<AiStreamChunk> {
    const request = this.buildCopilotRequest(message, true);
    return this.consumeStream(request);
  }

  search(searchQuery: string): Observable<AiChatResponse> {
    const request = this.buildCopilotRequest('', false, searchQuery);
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }

  chatWithRequest(request: AiCopilotChatRequest): Observable<AiChatResponse> {
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }

  listConversations(limit = 20): Observable<AiConversationSession[]> {
    return this.get<AiConversationSession[]>(API_ENDPOINTS.ai.conversations, { limit });
  }

  getConversation(sessionId: string): Observable<AiConversationMessage[]> {
    return this.get<AiConversationMessage[]>(`${API_ENDPOINTS.ai.conversation}/${sessionId}`);
  }

  getInsights(): Observable<AiProactiveInsight[]> {
    return this.get<AiProactiveInsight[]>(API_ENDPOINTS.ai.insights);
  }

  getDashboardCopilot(): Observable<AiDashboardCopilot> {
    return this.get<AiDashboardCopilot>(API_ENDPOINTS.ai.dashboardCopilot);
  }

  getDiagnostics(since?: string): Observable<AiDiagnosticsSummary> {
    return this.get<AiDiagnosticsSummary>(API_ENDPOINTS.ai.diagnostics, since ? { since } : undefined);
  }

  setSessionId(sessionId: string | null): void {
    this.sessionId = sessionId;
  }

  getSessionId(): string | null {
    return this.sessionId;
  }

  buildCopilotRequest(message: string, stream = false, searchQuery?: string): AiCopilotChatRequest {
    const base = this.aiContext.buildChatRequest(message, searchQuery ?? null);
    return {
      ...base,
      sessionId: this.sessionId,
      stream,
    };
  }

  private async *consumeStream(request: AiCopilotChatRequest): AsyncGenerator<AiStreamChunk> {
    const token = this.tokenService.getToken();
    const tenantId = this.tokenService.tenantId();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      Accept: 'text/event-stream',
    };
    if (token) headers[HTTP_HEADERS.authorization] = `Bearer ${token}`;
    if (tenantId) headers[HTTP_HEADERS.tenantId] = tenantId;

    const response = await fetch(`${environment.apiUrl}${API_ENDPOINTS.ai.chatStream}`, {
      method: 'POST',
      headers,
      body: JSON.stringify(request),
    });

    if (!response.ok || !response.body) {
      throw new Error('Streaming request failed');
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split('\n\n');
      buffer = parts.pop() ?? '';
      for (const part of parts) {
        const line = part.trim();
        if (!line.startsWith('data:')) continue;
        const json = line.slice(5).trim();
        if (!json) continue;
        yield JSON.parse(json) as AiStreamChunk;
      }
    }
  }
}
