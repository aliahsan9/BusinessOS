import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { BillingService } from '../../../core/services/billing.service';
import { BillingInvoiceDto } from '../../../core/models/billing.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';

@Component({
  selector: 'app-invoice-history',
  standalone: true,
  imports: [
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppCurrencyPipe,
  ],
  templateUrl: './invoice-history.component.html',
  styleUrl: './invoice-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceHistoryComponent implements OnInit {
  private readonly billingService = inject(BillingService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly invoices = signal<BillingInvoiceDto[]>([]);
  readonly breadcrumbs = [{ label: 'Billing', route: ROUTES.billing.base }, { label: 'Invoices' }];

  ngOnInit(): void {
    this.billingService.getInvoices().subscribe({
      next: (data) => {
        this.invoices.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load invoices.');
        this.loading.set(false);
      },
    });
  }
}
