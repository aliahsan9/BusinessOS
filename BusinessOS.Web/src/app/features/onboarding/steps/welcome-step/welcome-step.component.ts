import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { AppButtonComponent } from '../../../../shared/components/app-button/app-button.component';
import { ButtonVariant } from '../../../../core/enums';

@Component({
  selector: 'app-welcome-step',
  standalone: true,
  imports: [AppButtonComponent],
  templateUrl: './welcome-step.component.html',
  styleUrl: './welcome-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WelcomeStepComponent {
  readonly ButtonVariant = ButtonVariant;

  readonly next = output<void>();
  readonly skip = output<void>();
  readonly resumeLater = output<void>();

  readonly features = [
    { icon: '🤝', title: 'Customer Management', description: 'Organize contacts, track relationships, and view customer analytics.' },
    { icon: '📋', title: 'Project Management', description: 'Manage projects as orders with status tracking and revenue insights.' },
    { icon: '✅', title: 'Tasks', description: 'Break projects into line-item tasks with quantities and pricing.' },
    { icon: '🧾', title: 'Invoices', description: 'Generate invoices, track payments, and monitor outstanding balances.' },
    { icon: '💸', title: 'Expenses', description: 'Record and categorize business expenses with vendor tracking.' },
    { icon: '📈', title: 'Analytics', description: 'Visualize revenue, profit, and operational KPIs in real time.' },
    { icon: '📄', title: 'Reports', description: 'Export professional PDF reports for stakeholders and accounting.' },
  ];
}
