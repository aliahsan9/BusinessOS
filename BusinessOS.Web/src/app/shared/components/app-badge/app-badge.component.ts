import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type BadgeVariant = 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral';

@Component({
  selector: 'app-badge',
  standalone: true,
  templateUrl: './app-badge.component.html',
  styleUrl: './app-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppBadgeComponent {
  readonly label = input.required<string>();
  readonly variant = input<BadgeVariant>('neutral');
}
