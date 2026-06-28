import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FinanceService } from '../../../core/services/finance.service';
import { FinancialDashboard } from '../../../core/models/finance.model';
import { ChartDataResponse } from '../../../core/models/dashboard.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppChartComponent } from '../../../shared/components/app-chart/app-chart.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-financial-dashboard',
  standalone: true,
  imports: [
    CurrencyPipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppChartComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './financial-dashboard.component.html',
  styleUrl: './financial-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinancialDashboardComponent implements OnInit {
  private readonly financeService = inject(FinanceService);

  readonly dashboard = signal<FinancialDashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly routes = ROUTES;
  readonly breadcrumbs = [{ label: 'Finance', route: ROUTES.finance.dashboard }, { label: 'Dashboard' }];

  readonly revenueChart = computed<ChartDataResponse | null>(() => {
    const data = this.dashboard();
    if (!data?.revenueTrend?.length) return null;
    return {
      chartType: 'line',
      title: 'Revenue Trend',
      labels: data.revenueTrend.map((p) => p.period),
      datasets: [{ label: 'Revenue', data: data.revenueTrend.map((p) => p.amount), chartStyle: 'line' }],
      dateRange: { startDate: data.startDate, endDate: data.endDate, period: data.period },
    };
  });

  readonly expenseChart = computed<ChartDataResponse | null>(() => {
    const data = this.dashboard();
    if (!data?.expenseTrend?.length) return null;
    return {
      chartType: 'line',
      title: 'Expense Trend',
      labels: data.expenseTrend.map((p) => p.period),
      datasets: [{ label: 'Expenses', data: data.expenseTrend.map((p) => p.amount), chartStyle: 'line' }],
      dateRange: { startDate: data.startDate, endDate: data.endDate, period: data.period },
    };
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.financeService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load financial dashboard.');
        this.loading.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
