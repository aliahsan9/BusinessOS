import { Injectable, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AnalyticsPeriod } from '../core/enums';
import { AnalyticsService } from '../core/services/analytics.service';
import {
  AnalyticsKpiCard,
  AnalyticsOverviewResponse,
  AnalyticsProjectAnalyticsResponse,
  AnalyticsQueryParams,
  AnalyticsRecentActivityResponse,
  AnalyticsTaskAnalyticsResponse,
  AnalyticsTopCustomersResponse,
} from '../core/models/analytics.model';
import { ChartDataResponse } from '../core/models/dashboard.model';

export interface AnalyticsState {
  overview: AnalyticsOverviewResponse | null;
  revenueChart: ChartDataResponse | null;
  expenseChart: ChartDataResponse | null;
  profitChart: ChartDataResponse | null;
  customerGrowthChart: ChartDataResponse | null;
  projectAnalytics: AnalyticsProjectAnalyticsResponse | null;
  taskAnalytics: AnalyticsTaskAnalyticsResponse | null;
  topCustomers: AnalyticsTopCustomersResponse | null;
  recentActivity: AnalyticsRecentActivityResponse | null;
  loading: boolean;
  error: string | null;
  period: AnalyticsPeriod;
  customStartDate: string | null;
  customEndDate: string | null;
}

const initialState: AnalyticsState = {
  overview: null,
  revenueChart: null,
  expenseChart: null,
  profitChart: null,
  customerGrowthChart: null,
  projectAnalytics: null,
  taskAnalytics: null,
  topCustomers: null,
  recentActivity: null,
  loading: false,
  error: null,
  period: AnalyticsPeriod.Last30Days,
  customStartDate: null,
  customEndDate: null,
};

@Injectable({ providedIn: 'root' })
export class AnalyticsStateService {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly state = signal<AnalyticsState>(initialState);

  readonly overview = computed(() => this.state().overview);
  readonly revenueChart = computed(() => this.state().revenueChart);
  readonly expenseChart = computed(() => this.state().expenseChart);
  readonly profitChart = computed(() => this.state().profitChart);
  readonly customerGrowthChart = computed(() => this.state().customerGrowthChart);
  readonly projectAnalytics = computed(() => this.state().projectAnalytics);
  readonly taskAnalytics = computed(() => this.state().taskAnalytics);
  readonly topCustomers = computed(() => this.state().topCustomers);
  readonly recentActivity = computed(() => this.state().recentActivity);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);
  readonly period = computed(() => this.state().period);
  readonly customStartDate = computed(() => this.state().customStartDate);
  readonly customEndDate = computed(() => this.state().customEndDate);

  readonly kpiCards = computed((): AnalyticsKpiCard[] => {
    const overview = this.state().overview;
    if (!overview) return [];

    return [
      {
        key: 'customers',
        label: 'Total Customers',
        icon: '🤝',
        metric: overview.totalCustomers,
        format: 'number',
      },
      {
        key: 'projects',
        label: 'Active Projects',
        icon: '📁',
        metric: overview.activeProjects,
        format: 'number',
      },
      {
        key: 'tasks',
        label: 'Total Tasks',
        icon: '✅',
        metric: overview.totalTasks,
        format: 'number',
      },
      {
        key: 'completedTasks',
        label: 'Completed Tasks',
        icon: '🏁',
        metric: overview.completedTasks,
        format: 'number',
      },
      {
        key: 'revenue',
        label: 'Total Revenue',
        icon: '💰',
        metric: overview.totalRevenue,
        format: 'currency',
      },
      {
        key: 'expenses',
        label: 'Total Expenses',
        icon: '💸',
        metric: overview.totalExpenses,
        format: 'currency',
      },
      {
        key: 'profit',
        label: 'Net Profit',
        icon: '📈',
        metric: overview.netProfit,
        format: 'currency',
      },
      {
        key: 'invoices',
        label: 'Total Invoices',
        icon: '🧾',
        metric: overview.totalInvoices,
        format: 'number',
      },
    ];
  });

  loadAnalytics(
    period: AnalyticsPeriod = this.state().period,
    customStartDate?: string,
    customEndDate?: string,
  ): void {
    const params = this.buildParams(period, customStartDate, customEndDate);
    this.patchState({
      loading: true,
      error: null,
      period,
      customStartDate: customStartDate ?? null,
      customEndDate: customEndDate ?? null,
    });

    forkJoin({
      overview: this.analyticsService.getOverview(params),
      revenueChart: this.analyticsService.getRevenueChart(params),
      expenseChart: this.analyticsService.getExpenseChart(params),
      profitChart: this.analyticsService.getProfitChart(params),
      customerGrowthChart: this.analyticsService.getCustomerGrowthChart(params),
      projectAnalytics: this.analyticsService.getProjectAnalytics(params),
      taskAnalytics: this.analyticsService.getTaskAnalytics(params),
      topCustomers: this.analyticsService.getTopCustomers({ ...params, top: 10 }),
      recentActivity: this.analyticsService.getRecentActivity({ ...params, limit: 20 }),
    }).subscribe({
      next: (data) => {
        this.patchState({ ...data, loading: false, error: null });
      },
      error: () => {
        this.patchState({
          loading: false,
          error: 'Failed to load analytics data. Please try again.',
        });
      },
    });
  }

  setPeriod(period: AnalyticsPeriod): void {
    if (period === AnalyticsPeriod.Custom) {
      return;
    }
    this.loadAnalytics(period);
  }

  applyCustomRange(startDate: string, endDate: string): void {
    this.loadAnalytics(AnalyticsPeriod.Custom, startDate, endDate);
  }

  refresh(): void {
    const { period, customStartDate, customEndDate } = this.state();
    if (period === AnalyticsPeriod.Custom && customStartDate && customEndDate) {
      this.loadAnalytics(period, customStartDate, customEndDate);
      return;
    }
    this.loadAnalytics(period);
  }

  private buildParams(
    period: AnalyticsPeriod,
    customStartDate?: string,
    customEndDate?: string,
  ): AnalyticsQueryParams {
    if (period === AnalyticsPeriod.Custom && customStartDate && customEndDate) {
      return {
        period,
        startDate: customStartDate,
        endDate: customEndDate,
      };
    }
    return { period };
  }

  private patchState(partial: Partial<AnalyticsState>): void {
    this.state.update((current) => ({ ...current, ...partial }));
  }
}
