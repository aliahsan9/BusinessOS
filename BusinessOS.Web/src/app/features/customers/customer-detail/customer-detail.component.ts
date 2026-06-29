import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { CustomerService } from '../../../core/services/customer.service';
import { CustomerAnalytics, CustomerDto, CustomerOrderSummary } from '../../../core/models/customer.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [
    RouterLink,
    AppCurrencyPipe,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppPaginationComponent,
  ],
  templateUrl: './customer-detail.component.html',
  styleUrl: './customer-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly customerService = inject(CustomerService);
  private readonly tokenService = inject(TokenService);

  readonly customer = signal<CustomerDto | null>(null);
  readonly analytics = signal<CustomerAnalytics | null>(null);
  readonly orders = signal<CustomerOrderSummary[]>([]);
  readonly orderPage = signal(1);
  readonly orderTotalCount = signal(0);
  readonly orderTotalPages = signal(0);
  readonly loading = signal(true);
  readonly analyticsLoading = signal(false);
  readonly ordersLoading = signal(false);
  readonly routes = ROUTES;
  readonly canEdit = this.tokenService.hasPermission(PermissionCodes.customer.update);
  readonly canCreateOrder = this.tokenService.hasPermission(PermissionCodes.order.create);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.customerService.getById(id).subscribe({
      next: (c) => {
        this.customer.set(c);
        this.loading.set(false);
        this.loadAnalytics(id);
        this.loadOrders(id);
      },
      error: () => this.loading.set(false),
    });
  }

  loadAnalytics(customerId: string): void {
    this.analyticsLoading.set(true);
    this.customerService.getAnalytics(customerId).subscribe({
      next: (data) => {
        this.analytics.set(data);
        this.analyticsLoading.set(false);
      },
      error: () => this.analyticsLoading.set(false),
    });
  }

  loadOrders(customerId: string, page = 1): void {
    this.ordersLoading.set(true);
    this.customerService.getOrders(customerId, { page, pageSize: 5 }).subscribe({
      next: (result) => {
        this.orders.set(result.items);
        this.orderPage.set(result.page);
        this.orderTotalCount.set(result.totalCount);
        this.orderTotalPages.set(result.totalPages);
        this.ordersLoading.set(false);
      },
      error: () => this.ordersLoading.set(false),
    });
  }

  onOrderPageChange(page: number): void {
    const id = this.customer()?.id;
    if (id) this.loadOrders(id, page);
  }

  headerActions() {
    const actions = [];
    if (this.canCreateOrder) {
      actions.push({
        label: 'Create Order',
        route: `${ROUTES.orders.create}?customerId=${this.customer()?.id}`,
        icon: '🛒',
        variant: ButtonVariant.Primary,
      });
    }
    if (this.canEdit) {
      actions.push({
        label: 'Edit',
        route: `${ROUTES.customers.base}/${this.customer()?.id}/edit`,
        icon: '✏️',
        variant: ButtonVariant.Outline,
      });
    }
    return actions;
  }

  orderStatusVariant(status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Pending: 'warning',
      Confirmed: 'info',
      Processing: 'primary',
      Completed: 'success',
      Cancelled: 'danger',
    };
    return map[status] ?? 'neutral';
  }
}
