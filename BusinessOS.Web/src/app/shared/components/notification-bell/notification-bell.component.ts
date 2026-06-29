import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationStateService } from '../../../state/notification.state';
import { NotificationPanelComponent } from '../notification-panel/notification-panel.component';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [NotificationPanelComponent],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationBellComponent {
  readonly notificationState = inject(NotificationStateService);

  toggle(): void {
    this.notificationState.togglePanel();
  }
}
