import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { AiChatRequest, AiChatResponse } from '../models/ai.model';

@Injectable({ providedIn: 'root' })
export class AiService extends BaseApiService {
  chat(request: AiChatRequest): Observable<AiChatResponse> {
    return this.post<AiChatResponse>(API_ENDPOINTS.ai.chat, request);
  }
}
