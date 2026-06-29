import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BillingService } from '../../../core/services/billing.service';
import { BillingUsageDto, formatLimit, usagePercent } from '../../../core/models/billing.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-usage-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
  ],
  templateUrl: './usage-page.component.html',
  styleUrl: './usage-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsagePageComponent implements OnInit {
  readonly ROUTES = ROUTES;
  readonly formatLimit = formatLimit;
  readonly usagePercent = usagePercent;

  private readonly billingService = inject(BillingService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly usage = signal<BillingUsageDto | null>(null);
  readonly breadcrumbs = [{ label: 'Billing', route: ROUTES.billing.base }, { label: 'Usage' }];

  ngOnInit(): void {
    this.billingService.getUsage().subscribe({
      next: (data) => {
        this.usage.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load usage.');
        this.loading.set(false);
      },
    });
  }
}
