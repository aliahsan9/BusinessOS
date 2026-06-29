import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { AppButtonComponent } from '../../../../shared/components/app-button/app-button.component';
import { ButtonVariant } from '../../../../core/enums';

@Component({
  selector: 'app-analytics-intro-step',
  standalone: true,
  imports: [AppButtonComponent],
  templateUrl: './analytics-intro-step.component.html',
  styleUrl: './analytics-intro-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalyticsIntroStepComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly next = output<void>();
  readonly back = output<void>();
  readonly skipStep = output<void>();

  readonly insights = [
    { icon: '💰', title: 'Revenue Tracking', description: 'Monitor income trends, top customers, and sales performance over time.' },
    { icon: '💸', title: 'Expenses', description: 'Track spending by category and vendor to understand your cost structure.' },
    { icon: '📄', title: 'Reports', description: 'Generate PDF reports for business summary, P&L, and stakeholder updates.' },
    { icon: '📊', title: 'Dashboard Insights', description: 'Get a real-time overview of KPIs, alerts, and recent activity.' },
  ];
}
