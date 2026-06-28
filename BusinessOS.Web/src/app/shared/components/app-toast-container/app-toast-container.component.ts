import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationService } from '../../../core/services/notification.service';
import { ToastType } from '../../../core/enums';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  templateUrl: './app-toast-container.component.html',
  styleUrl: './app-toast-container.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppToastContainerComponent {
  private readonly notificationService = inject(NotificationService);
  readonly toasts = this.notificationService.toasts;

  dismiss(id: string): void {
    this.notificationService.remove(id);
  }

  getVariant(type: ToastType): string {
    return type;
  }
}
