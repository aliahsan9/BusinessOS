import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { InventoryStateService } from '../../../state/inventory.state';
import { StockMovementTrend } from '../../../core/models/inventory.model';
import { ChartDataResponse } from '../../../core/models/dashboard.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { StockTransactionType, ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppChartComponent } from '../../../shared/components/app-chart/app-chart.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-inventory-dashboard',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppChartComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './inventory-dashboard.component.html',
  styleUrl: './inventory-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryDashboardComponent implements OnInit {
  private readonly inventoryState = inject(InventoryStateService);

  readonly analytics = this.inventoryState.analytics;
  readonly analyticsLoading = this.inventoryState.analyticsLoadingState;
  readonly recentMovements = this.inventoryState.transactions;
  readonly movementsLoading = this.inventoryState.historyLoading;

  readonly movementChart = computed(() => {
    const trends = this.analytics()?.stockMovementTrends ?? [];
    return trends.length > 0 ? this.buildMovementChart(trends) : null;
  });

  readonly breadcrumbs = [
    { label: 'Inventory', route: ROUTES.inventory.overview },
    { label: 'Dashboard' },
  ];

  readonly headerActions = [
    { label: 'Overview', route: ROUTES.inventory.overview, icon: '🏠', variant: ButtonVariant.Outline },
    { label: 'Stock Levels', route: ROUTES.inventory.stockLevels, icon: '📊', variant: ButtonVariant.Outline },
  ];

  ngOnInit(): void {
    this.inventoryState.loadAnalytics();
    this.inventoryState.loadTransactions({ page: 1, pageSize: 10 });
  }

  retry(): void {
    this.inventoryState.loadAnalytics();
    this.inventoryState.loadTransactions({ page: 1, pageSize: 10 });
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

  private buildMovementChart(trends: StockMovementTrend[]): ChartDataResponse {
    return {
      chartType: 'line',
      title: 'Stock Movement Trends',
      labels: trends.map((t) => t.date),
      datasets: [
        { label: 'Stock In', data: trends.map((t) => t.totalIn), chartStyle: 'line' },
        { label: 'Stock Out', data: trends.map((t) => t.totalOut), chartStyle: 'line' },
      ],
      dateRange: { startDate: '', endDate: '', period: 'Last 30 days' },
    };
  }
}
