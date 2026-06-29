import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../../core/services/order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { OrderSummaryDto } from '../../../core/models/order.model';
import { ButtonVariant, OrderStatus } from '../../../core/enums';
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
  selector: 'app-order-list',
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
  templateUrl: './order-list.component.html',
  styleUrl: './order-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly orderService = inject(OrderService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<OrderSummaryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly statusFilter = signal('');
  readonly searchTerm = signal('');
  readonly deleteTarget = signal<OrderSummaryDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly OrderStatus = OrderStatus;
  readonly statusOptions = Object.values(OrderStatus);
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.order.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.order.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.order.delete);

  readonly breadcrumbs = [{ label: 'Orders', route: ROUTES.orders.list }, { label: 'All Orders' }];

  readonly headerActions = this.canCreate
    ? [{ label: 'New Order', route: ROUTES.orders.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.orderService
      .getAll({
        page,
        pageSize: this.pageSize(),
        status: (this.statusFilter() as OrderStatus) || undefined,
        search: this.searchTerm() || undefined,
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
          this.error.set('Failed to load orders.');
          this.loading.set(false);
        },
      });
  }

  onStatusFilter(status: string): void {
    this.statusFilter.set(status);
    this.load(1);
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  statusVariant(status: OrderStatus): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<OrderStatus, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [OrderStatus.Pending]: 'warning',
      [OrderStatus.Confirmed]: 'info',
      [OrderStatus.Processing]: 'primary',
      [OrderStatus.Completed]: 'success',
      [OrderStatus.Cancelled]: 'danger',
    };
    return map[status] ?? 'neutral';
  }

  canEditItem(order: OrderSummaryDto): boolean {
    return (
      this.canUpdate &&
      (order.status === OrderStatus.Pending || order.status === OrderStatus.Confirmed)
    );
  }

  canDeleteItem(order: OrderSummaryDto): boolean {
    return (
      this.canDelete &&
      order.status !== OrderStatus.Processing &&
      order.status !== OrderStatus.Completed
    );
  }

  confirmDelete(order: OrderSummaryDto): void {
    this.deleteTarget.set(order);
  }

  deleteOrder(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.orderService.remove(target.id).subscribe({
      next: () => {
        this.notification.success('Order deleted.');
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete order.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
