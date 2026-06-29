import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../../../../shared/components/app-button/app-button.component';
import { ButtonVariant } from '../../../../core/enums';
import { ROUTES } from '../../../../core/constants/route.constants';

@Component({
  selector: 'app-completion-step',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './completion-step.component.html',
  styleUrl: './completion-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompletionStepComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly routes = ROUTES;
  readonly finish = output<void>();

  readonly quickActions = [
    { label: 'Create Customer', route: ROUTES.customers.create, icon: '🤝' },
    { label: 'Create Project', route: ROUTES.orders.create, icon: '📋' },
    { label: 'Create Invoice', route: ROUTES.invoices.list, icon: '🧾' },
    { label: 'Open Dashboard', route: ROUTES.dashboard, icon: '📊' },
  ];
}
