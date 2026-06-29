import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, LowerCasePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BillingService } from '../../../core/services/billing.service';
import { BillingDashboardDto, formatLimit, usagePercent } from '../../../core/models/billing.model';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { TokenService } from '../../../core/services/token.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-billing-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    LowerCasePipe,
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppButtonComponent,
    AppCurrencyPipe,
  ],
  templateUrl: './billing-dashboard.component.html',
  styleUrl: './billing-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BillingDashboardComponent implements OnInit {
  readonly ROUTES = ROUTES;
  readonly ButtonVariant = ButtonVariant;
  readonly formatLimit = formatLimit;
  readonly usagePercent = usagePercent;

  private readonly billingService = inject(BillingService);
  private readonly tokenService = inject(TokenService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dashboard = signal<BillingDashboardDto | null>(null);
  readonly actionLoading = signal(false);
  readonly canManage = this.tokenService.hasPermission(PermissionCodes.subscription.manage);
  readonly breadcrumbs = [{ label: 'Billing' }];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.billingService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load billing dashboard.');
        this.loading.set(false);
      },
    });
  }

  openPortal(): void {
    this.actionLoading.set(true);
    this.billingService.createPortal(window.location.origin + ROUTES.billing.base).subscribe({
      next: (res) => {
        if (res.portalUrl) window.location.href = res.portalUrl;
        this.actionLoading.set(false);
      },
      error: () => {
        this.error.set('Unable to open billing portal. Configure Stripe or upgrade manually.');
        this.actionLoading.set(false);
      },
    });
  }

  cancelSubscription(): void {
    if (!confirm('Cancel subscription at end of billing period?')) return;
    this.actionLoading.set(true);
    this.billingService.cancelPlan(false).subscribe({
      next: () => this.load(),
      error: () => {
        this.error.set('Failed to cancel subscription.');
        this.actionLoading.set(false);
      },
    });
  }
}
