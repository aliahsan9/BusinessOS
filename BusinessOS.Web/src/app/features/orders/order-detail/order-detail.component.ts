import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { OrderService } from '../../../core/services/order.service';
import { InvoiceService } from '../../../core/services/invoice.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { OrderDto } from '../../../core/models/order.model';
import { ButtonVariant, OrderStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

type WorkflowAction = 'confirm' | 'process' | 'complete' | 'cancel' | 'invoice';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly orderService = inject(OrderService);
  private readonly invoiceService = inject(InvoiceService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly order = signal<OrderDto | null>(null);
  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly confirmAction = signal<WorkflowAction | null>(null);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.order.update);
  readonly canCreateInvoice = this.tokenService.hasPermission(PermissionCodes.invoice.create);

  ngOnInit(): void {
    this.loadOrder();
  }

  loadOrder(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.loading.set(true);
    this.orderService.getById(id).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    const order = this.order();
    if (!order || !this.canUpdate) return [];

    if (this.isEditable()) {
      return [
        {
          label: 'Edit',
          route: `${ROUTES.orders.base}/${order.id}/edit`,
          icon: '✏️',
          variant: ButtonVariant.Outline,
        },
      ];
    }
    return [];
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

  isEditable(): boolean {
    const status = this.order()?.status;
    return status === OrderStatus.Pending || status === OrderStatus.Confirmed;
  }

  canConfirm(): boolean {
    return this.canUpdate && this.order()?.status === OrderStatus.Pending;
  }

  canProcess(): boolean {
    return this.canUpdate && this.order()?.status === OrderStatus.Confirmed;
  }

  canComplete(): boolean {
    return this.canUpdate && this.order()?.status === OrderStatus.Processing;
  }

  canCancel(): boolean {
    const status = this.order()?.status;
    return (
      this.canUpdate &&
      !!status &&
      [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Processing].includes(status)
    );
  }

  canConvertToInvoice(): boolean {
    return this.canCreateInvoice && this.order()?.status === OrderStatus.Completed;
  }

  openConfirm(action: WorkflowAction): void {
    this.confirmAction.set(action);
  }

  confirmVariant(): ButtonVariant {
    const action = this.confirmAction();
    if (action === 'cancel') return ButtonVariant.Danger;
    return ButtonVariant.Primary;
  }

  confirmTitle(): string {
    const action = this.confirmAction();
    if (action === 'confirm') return 'Confirm Order';
    if (action === 'process') return 'Start Processing';
    if (action === 'complete') return 'Complete Order';
    if (action === 'invoice') return 'Create Invoice';
    return 'Cancel Order';
  }

  confirmMessage(): string {
    const action = this.confirmAction();
    if (action === 'confirm') return 'Mark this order as confirmed?';
    if (action === 'process') return 'Move this order to processing?';
    if (action === 'complete') return 'Mark this order as completed?';
    if (action === 'invoice') return 'Create an invoice from this order with a due date 30 days from today?';
    return 'Cancel this order? This cannot be undone.';
  }

  confirmLabel(): string {
    const action = this.confirmAction();
    if (action === 'cancel') return 'Cancel Order';
    if (action === 'invoice') return 'Create Invoice';
    return 'Confirm';
  }

  executeAction(): void {
    const order = this.order();
    const action = this.confirmAction();
    if (!order || !action) return;

    this.actionLoading.set(true);

    if (action === 'invoice') {
      const dueDate = new Date();
      dueDate.setDate(dueDate.getDate() + 30);
      this.invoiceService
        .createFromOrder(order.id, { dueDate: dueDate.toISOString().slice(0, 10) })
        .subscribe({
          next: (res) => {
            this.notification.success('Invoice created from order.');
            this.confirmAction.set(null);
            this.actionLoading.set(false);
            this.router.navigate([ROUTES.invoices.base, res.id]);
          },
          error: () => {
            this.notification.error('Failed to create invoice.');
            this.actionLoading.set(false);
          },
        });
      return;
    }

    const statusMap: Record<'confirm' | 'process' | 'complete' | 'cancel', OrderStatus> = {
      confirm: OrderStatus.Confirmed,
      process: OrderStatus.Processing,
      complete: OrderStatus.Completed,
      cancel: OrderStatus.Cancelled,
    };

    this.orderService.updateStatus(order.id, statusMap[action]).subscribe({
      next: () => {
        const messages = {
          confirm: 'Order confirmed.',
          process: 'Order is now processing.',
          complete: 'Order completed.',
          cancel: 'Order cancelled.',
        };
        this.notification.success(messages[action]);
        this.confirmAction.set(null);
        this.actionLoading.set(false);
        this.loadOrder();
      },
      error: () => {
        this.notification.error('Failed to update order status.');
        this.actionLoading.set(false);
      },
    });
  }
}
