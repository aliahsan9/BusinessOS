import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryStateService } from '../../../state/inventory.state';
import { InventoryService } from '../../../core/services/inventory.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { InventorySummary } from '../../../core/models/inventory.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { StockTransactionType, ButtonVariant } from '../../../core/enums';
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
  selector: 'app-stock-levels',
  standalone: true,
  imports: [
    RouterLink,
    DecimalPipe,
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
  templateUrl: './stock-levels.component.html',
  styleUrl: './stock-levels.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockLevelsComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly inventoryState = inject(InventoryStateService);
  private readonly inventoryService = inject(InventoryService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = this.inventoryState.items;
  readonly page = this.inventoryState.page;
  readonly pageSize = this.inventoryState.pageSize;
  readonly totalCount = this.inventoryState.totalCount;
  readonly totalPages = this.inventoryState.totalPages;
  readonly loading = this.inventoryState.loading;
  readonly error = this.inventoryState.error;

  readonly searchTerm = signal('');
  readonly stockFilter = signal('');
  readonly showAdjustModal = signal(false);
  readonly adjustLoading = signal(false);
  readonly selectedItem = signal<InventorySummary | null>(null);

  adjustQuantity = 0;
  adjustTransactionType = StockTransactionType.Adjustment;
  adjustNotes = '';
  adjustReference = '';

  readonly routes = ROUTES;
  readonly transactionTypes = Object.values(StockTransactionType);
  readonly canAdjust = this.tokenService.hasPermission(PermissionCodes.inventory.adjust);

  readonly breadcrumbs = [
    { label: 'Inventory', route: ROUTES.inventory.overview },
    { label: 'Stock Levels' },
  ];

  readonly headerActions = [
    { label: 'Overview', route: ROUTES.inventory.overview, icon: '🏠', variant: ButtonVariant.Outline },
  ];

  ngOnInit(): void {
    this.inventoryState.loadInventory();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.inventoryState.loadInventory({ search: term, page: 1 });
  }

  onStockFilter(filter: string): void {
    this.stockFilter.set(filter);
    this.inventoryState.loadInventory({
      lowStock: filter === 'low' ? true : undefined,
      outOfStock: filter === 'out' ? true : undefined,
      page: 1,
    });
  }

  onPageChange(page: number): void {
    this.inventoryState.loadInventory({ page });
  }

  openAdjustModal(item: InventorySummary): void {
    this.selectedItem.set(item);
    this.adjustQuantity = 0;
    this.adjustTransactionType = StockTransactionType.Adjustment;
    this.adjustNotes = '';
    this.adjustReference = '';
    this.showAdjustModal.set(true);
  }

  closeAdjustModal(): void {
    this.showAdjustModal.set(false);
    this.selectedItem.set(null);
  }

  submitAdjust(): void {
    const item = this.selectedItem();
    if (!item || this.adjustQuantity === 0) return;

    this.adjustLoading.set(true);
    this.inventoryService
      .adjustStock({
        productId: item.productId,
        quantity: this.adjustQuantity,
        transactionType: this.adjustTransactionType,
        notes: this.adjustNotes || null,
        referenceNumber: this.adjustReference || null,
      })
      .subscribe({
        next: () => {
          this.notification.success('Stock adjusted successfully.');
          this.adjustLoading.set(false);
          this.closeAdjustModal();
          this.inventoryState.loadInventory();
        },
        error: () => {
          this.notification.error('Failed to adjust stock.');
          this.adjustLoading.set(false);
        },
      });
  }

  getStockVariant(item: InventorySummary): 'success' | 'warning' | 'danger' | 'neutral' {
    if (item.isOutOfStock) return 'danger';
    if (item.isLowStock) return 'warning';
    return 'success';
  }

  getStockLabel(item: InventorySummary): string {
    if (item.isOutOfStock) return 'Out of Stock';
    if (item.isLowStock) return 'Low Stock';
    return 'In Stock';
  }

  retry(): void {
    this.inventoryState.loadInventory();
  }
}
