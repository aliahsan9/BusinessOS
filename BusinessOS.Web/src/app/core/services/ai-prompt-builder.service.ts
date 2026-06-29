import { Injectable } from '@angular/core';
import { AiPageContext } from '../models/ai.model';

@Injectable({ providedIn: 'root' })
export class AiPromptBuilderService {
  buildContextLabel(page: AiPageContext): string | null {
    if (page.customerId) return 'Answering in customer context';
    if (page.orderId) return 'Answering in project/order context';
    if (page.invoiceId) return 'Answering in invoice context';
    if (page.projectId) return 'Answering in project context';
    if (page.module !== 'general') return `On ${page.module} page`;
    return null;
  }
}
