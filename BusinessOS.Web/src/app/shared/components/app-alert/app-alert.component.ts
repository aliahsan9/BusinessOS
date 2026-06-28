import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export type AlertVariant = 'success' | 'danger' | 'warning' | 'info';

@Component({
  selector: 'app-alert',
  standalone: true,
  templateUrl: './app-alert.component.html',
  styleUrl: './app-alert.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppAlertComponent {
  readonly title = input<string>();
  readonly message = input.required<string>();
  readonly variant = input<AlertVariant>('info');
  readonly dismissible = input(false);

  readonly dismissed = output<void>();
}
