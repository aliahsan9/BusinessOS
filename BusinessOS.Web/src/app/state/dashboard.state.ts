import { Injectable, inject, signal, computed } from '@angular/core';
import { forkJoin } from 'rxjs';
import { DashboardService } from '../core/services/dashboard.service';
import {
  ChartDataResponse,
  CustomerAnalyticsDashboardResponse,
  DashboardOverviewResponse,
  DashboardQueryParams,
  InventoryAnalyticsDashboardResponse,
  OrderAnalyticsResponse,
  ProductAnalyticsResponse,
  SalesAnalyticsResponse,
} from '../core/models/dashboard.model';
import { DashboardPeriod } from '../core/enums';

export interface DashboardState {
  overview: DashboardOverviewResponse | null;
  sales: SalesAnalyticsResponse | null;
  customers: CustomerAnalyticsDashboardResponse | null;
  products: ProductAnalyticsResponse | null;
  inventory: InventoryAnalyticsDashboardResponse | null;
  orders: OrderAnalyticsResponse | null;
  revenueChart: ChartDataResponse | null;
  ordersChart: ChartDataResponse | null;
  loading: boolean;
  error: string | null;
  period: DashboardPeriod;
}

const initialState: DashboardState = {
  overview: null,
  sales: null,
  customers: null,
  products: null,
  inventory: null,
  orders: null,
  revenueChart: null,
  ordersChart: null,
  loading: false,
  error: null,
  period: DashboardPeriod.Month,
};

@Injectable({ providedIn: 'root' })
export class DashboardStateService {
  private readonly dashboardService = inject(DashboardService);
  private readonly state = signal<DashboardState>(initialState);

  readonly overview = computed(() => this.state().overview);
  readonly sales = computed(() => this.state().sales);
  readonly customers = computed(() => this.state().customers);
  readonly products = computed(() => this.state().products);
  readonly inventory = computed(() => this.state().inventory);
  readonly orders = computed(() => this.state().orders);
  readonly revenueChart = computed(() => this.state().revenueChart);
  readonly ordersChart = computed(() => this.state().ordersChart);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);
  readonly period = computed(() => this.state().period);

  loadDashboard(period: DashboardPeriod = DashboardPeriod.Month): void {
    const params: DashboardQueryParams = { period, top: 10 };
    this.patchState({ loading: true, error: null, period });

    forkJoin({
      overview: this.dashboardService.getOverview(params),
      sales: this.dashboardService.getSales(params),
      customers: this.dashboardService.getCustomers(params),
      products: this.dashboardService.getProducts(params),
      inventory: this.dashboardService.getInventory(params),
      orders: this.dashboardService.getOrders(params),
      revenueChart: this.dashboardService.getRevenueChart(params),
      ordersChart: this.dashboardService.getOrdersChart(params),
    }).subscribe({
      next: (data) => {
        this.patchState({
          ...data,
          loading: false,
          error: null,
        });
      },
      error: () => {
        this.patchState({
          loading: false,
          error: 'Failed to load dashboard data. Please try again.',
        });
      },
    });
  }

  setPeriod(period: DashboardPeriod): void {
    this.loadDashboard(period);
  }

  private patchState(partial: Partial<DashboardState>): void {
    this.state.update((current) => ({ ...current, ...partial }));
  }
}
