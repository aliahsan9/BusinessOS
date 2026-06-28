import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryStateService } from '../../../state/inventory.state';
import { ROUTES } from '../../../core/constants/route.constants';
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
  selector: 'app-stock-history',
  standalone: true,
  imports: [
    DatePipe,
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
  templateUrl: './stock-history.component.html',
  styleUrl: './stock-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockHistoryComponent implements OnInit {
  private readonly inventoryState = inject(InventoryStateService);

  readonly transactions = this.inventoryState.transactions;
  readonly page = this.inventoryState.historyPage;
  readonly totalCount = this.inventoryState.historyTotalCount;
  readonly totalPages = this.inventoryState.historyTotalPages;
  readonly loading = this.inventoryState.historyLoading;

  readonly searchTerm = signal('');
  readonly productIdFilter = signal('');
  readonly typeFilter = signal('');

  readonly transactionTypes = Object.values(StockTransactionType);

  readonly breadcrumbs = [
    { label: 'Inventory', route: ROUTES.inventory.overview },
    { label: 'Stock History' },
  ];

  readonly headerActions = [
    { label: 'Overview', route: ROUTES.inventory.overview, icon: '🏠', variant: ButtonVariant.Outline },
  ];

  ngOnInit(): void {
    this.inventoryState.loadTransactions();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.inventoryState.loadTransactions({ search: term, page: 1 });
  }

  onProductIdFilter(productId: string): void {
    this.productIdFilter.set(productId);
    this.inventoryState.loadTransactions({ productId: productId || undefined, page: 1 });
  }

  onTypeFilter(type: string): void {
    this.typeFilter.set(type);
    this.inventoryState.loadTransactions({ transactionType: type || undefined, page: 1 });
  }

  onPageChange(page: number): void {
    this.inventoryState.loadTransactions({ page });
  }

  getTypeVariant(type: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [StockTransactionType.Purchase]: 'success',
      [StockTransactionType.Sale]: 'primary',
      [StockTransactionType.Adjustment]: 'info',
      [StockTransactionType.Return]: 'success',
      [StockTransactionType.Damage]: 'danger',
      [StockTransactionType.Transfer]: 'warning',
    };
    return map[type] ?? 'neutral';
  }

  retry(): void {
    this.inventoryState.loadTransactions();
  }
}
