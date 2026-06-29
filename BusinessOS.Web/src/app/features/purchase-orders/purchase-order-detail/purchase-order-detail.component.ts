import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PurchaseOrderDto } from '../../../core/models/purchase-order.model';
import { PurchaseOrderStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-purchase-order-detail',
  standalone: true,
  imports: [
    RouterLink,
    AppCurrencyPipe,
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
  templateUrl: './purchase-order-detail.component.html',
  styleUrl: './purchase-order-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrderDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly purchaseOrder = signal<PurchaseOrderDto | null>(null);
  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly confirmAction = signal<'submit' | 'approve' | 'receive' | 'cancel' | null>(null);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.purchaseOrder.update);

  ngOnInit(): void {
    this.loadPurchaseOrder();
  }

  loadPurchaseOrder(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.loading.set(true);
    this.purchaseOrderService.getById(id).subscribe({
      next: (po) => {
        this.purchaseOrder.set(po);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    const po = this.purchaseOrder();
    if (!po || !this.canUpdate) return [];

    const actions = [];
    if (this.isEditable()) {
      actions.push({
        label: 'Edit',
        route: `${ROUTES.purchaseOrders.base}/${po.id}/edit`,
        icon: '✏️',
        variant: ButtonVariant.Outline,
      });
    }
    return actions;
  }

  statusVariant(status: PurchaseOrderStatus): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<PurchaseOrderStatus, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [PurchaseOrderStatus.Draft]: 'neutral',
      [PurchaseOrderStatus.Pending]: 'warning',
      [PurchaseOrderStatus.Approved]: 'info',
      [PurchaseOrderStatus.Received]: 'success',
      [PurchaseOrderStatus.Cancelled]: 'danger',
    };
    return map[status] ?? 'neutral';
  }

  isEditable(): boolean {
    const status = this.purchaseOrder()?.status;
    return status === PurchaseOrderStatus.Draft || status === PurchaseOrderStatus.Pending;
  }

  canSubmit(): boolean {
    return this.canUpdate && this.purchaseOrder()?.status === PurchaseOrderStatus.Draft;
  }

  canApprove(): boolean {
    return this.canUpdate && this.purchaseOrder()?.status === PurchaseOrderStatus.Pending;
  }

  canReceive(): boolean {
    return this.canUpdate && this.purchaseOrder()?.status === PurchaseOrderStatus.Approved;
  }

  canCancel(): boolean {
    const status = this.purchaseOrder()?.status;
    return (
      this.canUpdate &&
      !!status &&
      [PurchaseOrderStatus.Draft, PurchaseOrderStatus.Pending, PurchaseOrderStatus.Approved].includes(status)
    );
  }

  openConfirm(action: 'submit' | 'approve' | 'receive' | 'cancel'): void {
    this.confirmAction.set(action);
  }

  confirmVariant(): ButtonVariant {
    return this.confirmAction() === 'cancel' ? ButtonVariant.Danger : ButtonVariant.Primary;
  }

  executeAction(): void {
    const po = this.purchaseOrder();
    const action = this.confirmAction();
    if (!po || !action) return;

    this.actionLoading.set(true);

    if (action === 'receive') {
      this.purchaseOrderService.receive(po.id).subscribe({
        next: () => this.onActionSuccess('Purchase order received.'),
        error: () => this.onActionError('Failed to receive purchase order.'),
      });
      return;
    }

    const statusMap: Record<'submit' | 'approve' | 'cancel', PurchaseOrderStatus> = {
      submit: PurchaseOrderStatus.Pending,
      approve: PurchaseOrderStatus.Approved,
      cancel: PurchaseOrderStatus.Cancelled,
    };

    this.purchaseOrderService.updateStatus(po.id, statusMap[action]).subscribe({
      next: () => {
        const messages = {
          submit: 'Purchase order submitted for approval.',
          approve: 'Purchase order approved.',
          cancel: 'Purchase order cancelled.',
        };
        this.onActionSuccess(messages[action]);
      },
      error: () => this.onActionError('Failed to update purchase order status.'),
    });
  }

  confirmTitle(): string {
    const action = this.confirmAction();
    if (action === 'submit') return 'Submit Purchase Order';
    if (action === 'approve') return 'Approve Purchase Order';
    if (action === 'receive') return 'Receive Purchase Order';
    return 'Cancel Purchase Order';
  }

  confirmMessage(): string {
    const action = this.confirmAction();
    if (action === 'submit') return 'Submit this purchase order for approval?';
    if (action === 'approve') return 'Approve this purchase order?';
    if (action === 'receive') return 'Mark this purchase order as received? Stock will be updated.';
    return 'Cancel this purchase order?';
  }

  private onActionSuccess(message: string): void {
    this.notification.success(message);
    this.confirmAction.set(null);
    this.actionLoading.set(false);
    this.loadPurchaseOrder();
  }

  private onActionError(message: string): void {
    this.notification.error(message);
    this.actionLoading.set(false);
  }
}
