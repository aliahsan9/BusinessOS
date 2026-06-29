import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { FormsModule } from '@angular/forms';
import { QuotationService } from '../../../core/services/quotation.service';
import { CustomerService } from '../../../core/services/customer.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { QuotationSummaryDto } from '../../../core/models/quotation.model';
import { CustomerSummaryDto } from '../../../core/models/customer.model';
import { ButtonVariant, QuotationStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-quotation-list',
  standalone: true,
  imports: [
    RouterLink,
    AppCurrencyPipe,
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
    AppConfirmDialogComponent,
  ],
  templateUrl: './quotation-list.component.html',
  styleUrl: './quotation-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuotationListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly quotationService = inject(QuotationService);
  private readonly customerService = inject(CustomerService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<QuotationSummaryDto[]>([]);
  readonly customers = signal<CustomerSummaryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly statusFilter = signal('');
  readonly customerFilter = signal('');
  readonly deleteTarget = signal<QuotationSummaryDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly QuotationStatus = QuotationStatus;
  readonly statusOptions = Object.values(QuotationStatus);
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.quotation.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.quotation.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.quotation.delete);

  readonly breadcrumbs = [{ label: 'Quotations', route: ROUTES.quotations.list }, { label: 'All Quotations' }];

  readonly headerActions = this.canCreate
    ? [{ label: 'New Quotation', route: ROUTES.quotations.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
    this.customerService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.customers.set(result.items),
    });
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.quotationService
      .getAll({
        page,
        pageSize: this.pageSize(),
        search: this.searchTerm() || undefined,
        status: (this.statusFilter() as QuotationStatus) || undefined,
        customerId: this.customerFilter() || undefined,
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
          this.error.set('Failed to load quotations.');
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

  onCustomerFilter(customerId: string): void {
    this.customerFilter.set(customerId);
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  statusVariant(status: QuotationStatus): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<QuotationStatus, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [QuotationStatus.Draft]: 'neutral',
      [QuotationStatus.Sent]: 'info',
      [QuotationStatus.Accepted]: 'success',
      [QuotationStatus.Rejected]: 'danger',
      [QuotationStatus.Expired]: 'warning',
    };
    return map[status] ?? 'neutral';
  }

  canEditItem(quotation: QuotationSummaryDto): boolean {
    return (
      this.canUpdate &&
      (quotation.status === QuotationStatus.Draft || quotation.status === QuotationStatus.Sent)
    );
  }

  canDeleteItem(quotation: QuotationSummaryDto): boolean {
    return (
      this.canDelete &&
      (quotation.status === QuotationStatus.Draft ||
        quotation.status === QuotationStatus.Rejected ||
        quotation.status === QuotationStatus.Expired)
    );
  }

  confirmDelete(quotation: QuotationSummaryDto): void {
    this.deleteTarget.set(quotation);
  }

  deleteQuotation(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.quotationService.remove(target.id).subscribe({
      next: () => {
        this.notification.success('Quotation deleted.');
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete quotation.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
