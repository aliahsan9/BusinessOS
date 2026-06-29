import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AiChatRequest, AiChatResponse } from '../models/ai.model';
import { AiChatService } from './ai-chat.service';

/** @deprecated Use AiChatService directly */
@Injectable({ providedIn: 'root' })
export class AiService {
  private readonly aiChatService = inject(AiChatService);

  chat(request: AiChatRequest): Observable<AiChatResponse> {
    return this.aiChatService.chatWithRequest(request);
  }
}
