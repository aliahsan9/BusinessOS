import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../app-button/app-button.component';
import { ButtonVariant } from '../../../core/enums';

export interface PageHeaderAction {
  label: string;
  route?: string;
  icon?: string;
  variant?: ButtonVariant;
  action?: () => void;
}

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './app-page-header.component.html',
  styleUrl: './app-page-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppPageHeaderComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly actions = input<PageHeaderAction[]>([]);

  readonly actionClick = output<PageHeaderAction>();

  onAction(action: PageHeaderAction): void {
    if (action.action) {
      action.action();
    }
    this.actionClick.emit(action);
  }
}
