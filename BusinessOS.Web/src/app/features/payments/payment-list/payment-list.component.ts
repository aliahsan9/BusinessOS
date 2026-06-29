import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { FormsModule } from '@angular/forms';
import { PaymentService } from '../../../core/services/payment.service';
import { CustomerService } from '../../../core/services/customer.service';
import { OrderService } from '../../../core/services/order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { PaymentSummaryDto } from '../../../core/models/payment.model';
import { CustomerSummaryDto } from '../../../core/models/customer.model';
import { OrderSummaryDto } from '../../../core/models/order.model';
import { ButtonVariant, PaymentMethod } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-payment-list',
  standalone: true,
  imports: [
    RouterLink,
    AppCurrencyPipe,
    DatePipe,
    FormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './payment-list.component.html',
  styleUrl: './payment-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly paymentService = inject(PaymentService);
  private readonly customerService = inject(CustomerService);
  private readonly orderService = inject(OrderService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<PaymentSummaryDto[]>([]);
  readonly customers = signal<CustomerSummaryDto[]>([]);
  readonly orders = signal<OrderSummaryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly customerFilter = signal('');
  readonly orderFilter = signal('');
  readonly methodFilter = signal('');
  readonly deleteTarget = signal<PaymentSummaryDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly PaymentMethod = PaymentMethod;
  readonly methodOptions = Object.values(PaymentMethod);
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.payment.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.payment.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.payment.delete);

  readonly breadcrumbs = [{ label: 'Payments', route: ROUTES.payments.list }, { label: 'All Payments' }];

  readonly headerActions = this.canCreate
    ? [{ label: 'Record Payment', route: ROUTES.payments.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
    this.customerService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.customers.set(result.items),
    });
    this.orderService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.orders.set(result.items),
    });
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.paymentService
      .getAll({
        page,
        pageSize: this.pageSize(),
        customerId: this.customerFilter() || undefined,
        orderId: this.orderFilter() || undefined,
        paymentMethod: (this.methodFilter() as PaymentMethod) || undefined,
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
          this.error.set('Failed to load payments.');
          this.loading.set(false);
        },
      });
  }

  onCustomerFilter(customerId: string): void {
    this.customerFilter.set(customerId);
    this.load(1);
  }

  onOrderFilter(orderId: string): void {
    this.orderFilter.set(orderId);
    this.load(1);
  }

  onMethodFilter(method: string): void {
    this.methodFilter.set(method);
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  confirmDelete(payment: PaymentSummaryDto): void {
    this.deleteTarget.set(payment);
  }

  deletePayment(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.paymentService.remove(target.id).subscribe({
      next: () => {
        this.notification.success('Payment deleted.');
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete payment.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
