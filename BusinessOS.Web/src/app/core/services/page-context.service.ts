import { Injectable, signal } from '@angular/core';
import { AiPageContext } from '../models/ai.model';

@Injectable({ providedIn: 'root' })
export class PageContextService {
  private readonly _context = signal<AiPageContext | null>(null);
  readonly context = this._context.asReadonly();

  setContext(context: AiPageContext | null): void {
    this._context.set(context);
  }

  clearContext(): void {
    this._context.set(null);
  }
}
