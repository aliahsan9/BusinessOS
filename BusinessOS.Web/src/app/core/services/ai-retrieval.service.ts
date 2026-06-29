import { Injectable } from '@angular/core';
import { AiRetrievedSources } from '../models/ai.model';

@Injectable({ providedIn: 'root' })
export class AiRetrievalService {
  formatSourcesSummary(sources: AiRetrievedSources): string {
    const parts: string[] = [];

    if (sources.customers > 0) parts.push(`Customers (${sources.customers})`);
    if (sources.orders > 0) parts.push(`Orders (${sources.orders})`);
    if (sources.invoices > 0) parts.push(`Invoices (${sources.invoices})`);
    if (sources.projects > 0) parts.push(`Projects (${sources.projects})`);

    return parts.length ? parts.join(', ') : 'No entity data retrieved';
  }

  hasRetrievedData(sources: AiRetrievedSources): boolean {
    return sources.customers + sources.orders + sources.invoices + sources.projects > 0;
  }
}
