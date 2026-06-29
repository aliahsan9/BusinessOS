import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { FormsModule } from '@angular/forms';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { PurchaseOrderDto } from '../../../core/models/purchase-order.model';
import { SupplierDto } from '../../../core/models/supplier.model';
import { ButtonVariant,  PurchaseOrderStatus  } from '../../../core/enums';
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
  selector: 'app-purchase-order-list',
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
  templateUrl: './purchase-order-list.component.html',
  styleUrl: './purchase-order-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrderListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly supplierService = inject(SupplierService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<PurchaseOrderDto[]>([]);
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly statusFilter = signal('');
  readonly supplierFilter = signal('');
  readonly deleteTarget = signal<PurchaseOrderDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly PurchaseOrderStatus = PurchaseOrderStatus;
  readonly statusOptions = Object.values(PurchaseOrderStatus);
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.purchaseOrder.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.purchaseOrder.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.purchaseOrder.delete);

  readonly breadcrumbs = [
    { label: 'Purchase Orders', route: ROUTES.purchaseOrders.list },
    { label: 'All Orders' },
  ];

  readonly headerActions = this.canCreate
    ? [{ label: 'New Purchase Order', route: ROUTES.purchaseOrders.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
    this.supplierService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.suppliers.set(result.items),
    });
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.purchaseOrderService
      .getAll({
        page,
        pageSize: this.pageSize(),
        status: (this.statusFilter() as PurchaseOrderStatus) || undefined,
        supplierId: this.supplierFilter() || undefined,
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
          this.error.set('Failed to load purchase orders.');
          this.loading.set(false);
        },
      });
  }

  onStatusFilter(status: string): void {
    this.statusFilter.set(status);
    this.load(1);
  }

  onSupplierFilter(supplierId: string): void {
    this.supplierFilter.set(supplierId);
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
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

  canEditItem(po: PurchaseOrderDto): boolean {
    return (
      this.canUpdate &&
      (po.status === PurchaseOrderStatus.Draft || po.status === PurchaseOrderStatus.Pending)
    );
  }

  confirmDelete(po: PurchaseOrderDto): void {
    this.deleteTarget.set(po);
  }

  deletePurchaseOrder(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.purchaseOrderService.remove(target.id).subscribe({
      next: () => {
        this.notification.success('Purchase order deleted.');
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete purchase order.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
