import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InvoiceService } from '../../../core/services/invoice.service';
import { InvoiceSummaryDto } from '../../../core/models/invoice.model';
import { ButtonVariant, InvoiceStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    DatePipe,
    FormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
  ],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly invoiceService = inject(InvoiceService);

  readonly items = signal<InvoiceSummaryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly statusFilter = signal('');

  readonly routes = ROUTES;
  readonly InvoiceStatus = InvoiceStatus;
  readonly statusOptions = Object.values(InvoiceStatus);

  readonly breadcrumbs = [{ label: 'Invoices', route: ROUTES.invoices.list }, { label: 'All Invoices' }];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.invoiceService
      .getAll({
        page,
        pageSize: this.pageSize(),
        search: this.searchTerm() || undefined,
        status: (this.statusFilter() as InvoiceStatus) || undefined,
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load invoices.');
          this.loading.set(false);
        },
      });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.load(1);
  }

  onStatusFilter(status: string): void {
    this.statusFilter.set(status);
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  statusVariant(status: InvoiceStatus): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<InvoiceStatus, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [InvoiceStatus.Draft]: 'neutral',
      [InvoiceStatus.Sent]: 'info',
      [InvoiceStatus.Paid]: 'success',
      [InvoiceStatus.PartiallyPaid]: 'warning',
      [InvoiceStatus.Overdue]: 'danger',
      [InvoiceStatus.Cancelled]: 'neutral',
    };
    return map[status] ?? 'neutral';
  }

  retry(): void {
    this.load();
  }
}
