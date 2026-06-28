import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonSize, ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-button',
  standalone: true,
  templateUrl: './app-button.component.html',
  styleUrl: './app-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppButtonComponent {
  readonly label = input.required<string>();
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly variant = input<ButtonVariant>(ButtonVariant.Primary);
  readonly size = input<ButtonSize>(ButtonSize.Md);
  readonly loading = input(false);
  readonly disabled = input(false);
  readonly icon = input<string>();
  readonly fullWidth = input(false);
  readonly ariaLabel = input<string>();

  readonly clicked = output<MouseEvent>();

  onClick(event: MouseEvent): void {
    if (!this.loading() && !this.disabled()) {
      this.clicked.emit(event);
    }
  }
}
