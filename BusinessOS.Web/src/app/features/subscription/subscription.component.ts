import { ChangeDetectionStrategy, Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AppCardComponent } from '../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../shared/components/app-skeleton/app-skeleton.component';
import { AppButtonComponent } from '../../shared/components/app-button/app-button.component';
import { AppCurrencyPipe } from '../../shared/pipes/app-currency.pipe';
import { ButtonVariant } from '../../core/enums';

interface SubscriptionPlan {
  tier: string;
  name: string;
  monthlyPrice: number;
  features: string[];
}

interface SubscriptionStatus {
  planTier: string;
  status: string;
  currentPlan: SubscriptionPlan;
  usage: {
    customersCount: number;
    maxCustomers: number;
    projectsCount: number;
    maxProjects: number;
  };
}

interface BillingHistoryItem {
  id: string;
  createdAt: string;
  eventType: string;
  planTier: string;
  amount: number;
  status: string;
}

@Component({
  selector: 'app-subscription',
  standalone: true,
  imports: [DatePipe, AppCardComponent, AppSkeletonComponent, AppButtonComponent, AppCurrencyPipe],
  templateUrl: './subscription.component.html',
  styleUrl: './subscription.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubscriptionComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  readonly loading = signal(false);
  readonly status = signal<SubscriptionStatus | null>(null);
  readonly plans = signal<SubscriptionPlan[]>([]);
  readonly billingHistory = signal<BillingHistoryItem[]>([]);

  ngOnInit(): void {
    this.plans.set([
      {
        tier: 'Free',
        name: 'Free',
        monthlyPrice: 0,
        features: ['Basic features', 'Limited usage'],
      },
      {
        tier: 'Pro',
        name: 'Pro',
        monthlyPrice: 49,
        features: ['Advanced analytics', 'Priority support'],
      },
    ]);
  }

  upgrade(_tier: string): void {
    // Placeholder for subscription upgrade flow.
  }

  planButtonVariant(planTier: string): ButtonVariant {
    return this.status()?.planTier === planTier ? ButtonVariant.Secondary : ButtonVariant.Primary;
  }
}
