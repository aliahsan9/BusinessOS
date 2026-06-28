import { Injectable, signal } from '@angular/core';
import { ToastType } from '../enums';

export interface ToastMessage {
  id: string;
  type: ToastType;
  title: string;
  message?: string;
  duration?: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly toastsSignal = signal<ToastMessage[]>([]);
  readonly toasts = this.toastsSignal.asReadonly();

  success(title: string, message?: string, duration = 4000): void {
    this.show(ToastType.Success, title, message, duration);
  }

  error(title: string, message?: string, duration = 6000): void {
    this.show(ToastType.Error, title, message, duration);
  }

  warning(title: string, message?: string, duration = 5000): void {
    this.show(ToastType.Warning, title, message, duration);
  }

  info(title: string, message?: string, duration = 4000): void {
    this.show(ToastType.Info, title, message, duration);
  }

  remove(id: string): void {
    this.toastsSignal.update((toasts) => toasts.filter((t) => t.id !== id));
  }

  private show(type: ToastType, title: string, message?: string, duration = 4000): void {
    const toast: ToastMessage = {
      id: crypto.randomUUID(),
      type,
      title,
      message,
      duration,
    };
    this.toastsSignal.update((toasts) => [...toasts, toast]);

    if (duration > 0) {
      setTimeout(() => this.remove(toast.id), duration);
    }
  }
}
