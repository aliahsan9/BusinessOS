import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AiChatRequest, AiPageContext } from '../models/ai.model';
import { PageContextService } from './page-context.service';

@Injectable({ providedIn: 'root' })
export class AiContextService {
  private readonly router = inject(Router);
  private readonly pageContext = inject(PageContextService);

  buildPageContext(): AiPageContext {
    const registered = this.pageContext.context();
    const url = this.router.url;
    const lower = url.toLowerCase();

    if (registered) {
      return { ...registered, url };
    }

    return {
      url,
      module: this.resolveModule(lower),
      customerId: this.extractId(lower, 'customers'),
      orderId: this.extractId(lower, 'orders'),
      invoiceId: this.extractId(lower, 'invoices'),
      projectId: null,
    };
  }

  buildChatRequest(message: string, searchQuery?: string | null): AiChatRequest {
    const page = this.buildPageContext();

    return {
      message,
      currentPage: page.url,
      searchQuery: searchQuery ?? null,
      customerId: page.customerId ?? null,
      orderId: page.orderId ?? null,
      invoiceId: page.invoiceId ?? null,
      projectId: page.projectId ?? null,
    };
  }

  private resolveModule(url: string): string {
    if (url.includes('customer')) return 'customers';
    if (url.includes('order')) return 'orders';
    if (url.includes('invoice')) return 'invoices';
    if (url.includes('project')) return 'projects';
    if (url.includes('expense')) return 'expenses';
    if (url.includes('analytics')) return 'analytics';
    if (url.includes('report')) return 'reports';
    if (url.includes('dashboard')) return 'dashboard';
    if (url.includes('setting')) return 'settings';
    return 'general';
  }

  private extractId(url: string, segment: string): string | null {
    const marker = `/${segment}/`;
    const idx = url.indexOf(marker);
    if (idx < 0) return null;

    const start = idx + marker.length;
    const end = url.indexOf('/', start);
    const raw = end < 0 ? url.slice(start) : url.slice(start, end);
    if (!raw || raw === 'new') return null;

    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return guidPattern.test(raw) ? raw : null;
  }
}
