import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { AiChatRequest, AiChatResponse } from '../models/ai.model';
import { AiContextService } from './ai-context.service';

@Injectable({ providedIn: 'root' })
export class AiChatService extends BaseApiService {
  private readonly aiContext = inject(AiContextService);

  chat(message: string): Observable<AiChatResponse> {
    const request = this.aiContext.buildChatRequest(message);
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }

  search(searchQuery: string): Observable<AiChatResponse> {
    const request = this.aiContext.buildChatRequest('', searchQuery);
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }

  chatWithRequest(request: AiChatRequest): Observable<AiChatResponse> {
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }
}
