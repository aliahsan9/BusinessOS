import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BillingService } from '../../../core/services/billing.service';
import {
  CurrentPlanDto,
  SubscriptionPlanDto,
} from '../../../core/models/billing.model';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { TokenService } from '../../../core/services/token.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-pricing-page',
  standalone: true,
  imports: [
    RouterLink,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppButtonComponent,
    AppCurrencyPipe,
  ],
  templateUrl: './pricing-page.component.html',
  styleUrl: './pricing-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PricingPageComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  readonly ROUTES = ROUTES;

  private readonly billingService = inject(BillingService);
  private readonly tokenService = inject(TokenService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly plans = signal<SubscriptionPlanDto[]>([]);
  readonly currentPlan = signal<CurrentPlanDto | null>(null);
  readonly annual = signal(false);
  readonly actionLoading = signal<string | null>(null);
  readonly canManage = this.tokenService.hasPermission(PermissionCodes.subscription.manage);

  ngOnInit(): void {
    this.billingService.getPlans().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load pricing plans.');
        this.loading.set(false);
      },
    });

    if (this.tokenService.isAuthenticated()) {
      this.billingService.getCurrentPlan().subscribe({
        next: (plan) => this.currentPlan.set(plan),
        error: () => undefined,
      });
    }
  }

  isAuthenticated(): boolean {
    return this.tokenService.isAuthenticated();
  }

  toggleBilling(): void {
    this.annual.update((v) => !v);
  }

  price(plan: SubscriptionPlanDto): number {
    return this.annual() ? plan.annualPrice : plan.monthlyPrice;
  }

  isCurrentPlan(plan: SubscriptionPlanDto): boolean {
    return this.currentPlan()?.planSlug === plan.slug;
  }

  selectPlan(plan: SubscriptionPlanDto): void {
    if (!this.canManage) return;
    if (this.isCurrentPlan(plan)) return;

    this.actionLoading.set(plan.id);
    const interval = this.annual() ? 'yearly' : 'monthly';

    this.billingService.checkout({
      planId: plan.id,
      billingInterval: interval,
      provider: 'stripe',
      successUrl: window.location.origin + ROUTES.billing.base + '?success=true',
      cancelUrl: window.location.origin + ROUTES.pricing.base + '?cancelled=true',
    }).subscribe({
      next: (session) => {
        if (session.checkoutUrl) {
          window.location.href = session.checkoutUrl;
        } else {
          this.billingService.upgradePlan({ planId: plan.id, billingInterval: interval }).subscribe({
            next: (updated) => {
              this.currentPlan.set(updated);
              this.actionLoading.set(null);
            },
            error: () => {
              this.error.set('Checkout unavailable. Plan upgraded locally (sandbox mode).');
              this.actionLoading.set(null);
            },
          });
        }
      },
      error: () => {
        this.error.set('Unable to start checkout.');
        this.actionLoading.set(null);
      },
    });
  }
}
