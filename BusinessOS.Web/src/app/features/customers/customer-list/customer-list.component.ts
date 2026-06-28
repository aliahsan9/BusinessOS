import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CustomerService } from '../../../core/services/customer.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { CustomerSummaryDto } from '../../../core/models/customer.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
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
  selector: 'app-customer-list',
  standalone: true,
  imports: [
    RouterLink,
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
  templateUrl: './customer-list.component.html',
  styleUrl: './customer-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly customerService = inject(CustomerService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<CustomerSummaryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly cityFilter = signal('');
  readonly countryFilter = signal('');
  readonly activeFilter = signal('');
  readonly sortBy = signal('createdAt');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly deleteTarget = signal<CustomerSummaryDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.customer.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.customer.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.customer.delete);

  readonly breadcrumbs = [{ label: 'Customers', route: ROUTES.customers.list }, { label: 'Directory' }];
  readonly headerActions = this.canCreate
    ? [{ label: 'Add Customer', route: ROUTES.customers.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.customerService
      .getAll({
        page,
        pageSize: this.pageSize(),
        search: this.searchTerm() || undefined,
        city: this.cityFilter() || undefined,
        country: this.countryFilter() || undefined,
        sortBy: this.sortBy(),
        sortDirection: this.sortDirection(),
      })
      .subscribe({
        next: (result) => {
          let items = result.items;
          const active = this.activeFilter();
          if (active === 'active') items = items.filter((c) => c.isActive);
          if (active === 'inactive') items = items.filter((c) => !c.isActive);
          this.items.set(items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load customers.');
          this.loading.set(false);
        },
      });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.load(1);
  }

  onFilterChange(): void {
    this.load(1);
  }

  toggleSort(column: string): void {
    if (this.sortBy() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(column);
      this.sortDirection.set('asc');
    }
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  confirmDelete(customer: CustomerSummaryDto): void {
    this.deleteTarget.set(customer);
  }

  deleteCustomer(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.deleting.set(true);
    this.customerService.remove(target.id).subscribe({
      next: () => {
        this.notification.success(`Deleted ${target.fullName}.`);
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete customer.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
