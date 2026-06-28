import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppButtonComponent } from '../app-button/app-button.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [AppButtonComponent],
  templateUrl: './app-empty-state.component.html',
  styleUrl: './app-empty-state.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppEmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
  readonly icon = input('📭');
  readonly actionLabel = input<string>();
  readonly actionVariant = input<ButtonVariant>(ButtonVariant.Primary);

  readonly action = output<void>();
}
