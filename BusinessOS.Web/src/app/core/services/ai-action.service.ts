import { Injectable } from '@angular/core';
import { AiActionResult } from '../models/ai.model';

@Injectable({ providedIn: 'root' })
export class AiActionService {
  formatActionMessage(action: AiActionResult): string {
    if (action.success) {
      return action.message;
    }
    return `Action failed: ${action.message}`;
  }

  shouldNavigate(action: AiActionResult): string | null {
    return action.success && action.route ? action.route : null;
  }
}
