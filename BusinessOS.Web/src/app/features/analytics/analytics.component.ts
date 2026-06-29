import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { AnalyticsPeriod } from '../../core/enums';
import { AnalyticsStateService } from '../../state/analytics.state';
import { AppBreadcrumbComponent } from '../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppCardComponent } from '../../shared/components/app-card/app-card.component';
import { AppChartComponent } from '../../shared/components/app-chart/app-chart.component';
import { AppSkeletonComponent } from '../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../shared/components/app-empty-state/app-empty-state.component';
import { AppBadgeComponent } from '../../shared/components/app-badge/app-badge.component';
import { AppCurrencyPipe } from '../../shared/pipes/app-currency.pipe';
import { AnalyticsKpiCard } from '../../core/models/analytics.model';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    AppCurrencyPipe,
    AppBreadcrumbComponent,
    AppCardComponent,
    AppChartComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
    AppBadgeComponent,
  ],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalyticsComponent implements OnInit {
  private readonly analyticsState = inject(AnalyticsStateService);

  readonly overview = this.analyticsState.overview;
  readonly kpiCards = this.analyticsState.kpiCards;
  readonly revenueChart = this.analyticsState.revenueChart;
  readonly expenseChart = this.analyticsState.expenseChart;
  readonly profitChart = this.analyticsState.profitChart;
  readonly customerGrowthChart = this.analyticsState.customerGrowthChart;
  readonly projectAnalytics = this.analyticsState.projectAnalytics;
  readonly taskAnalytics = this.analyticsState.taskAnalytics;
  readonly topCustomers = this.analyticsState.topCustomers;
  readonly recentActivity = this.analyticsState.recentActivity;
  readonly loading = this.analyticsState.loading;
  readonly error = this.analyticsState.error;
  readonly period = this.analyticsState.period;

  readonly showCustomRange = signal(false);
  readonly customStart = signal('');
  readonly customEnd = signal('');

  readonly periods = [
    { label: 'Today', value: AnalyticsPeriod.Today },
    { label: 'Last 7 Days', value: AnalyticsPeriod.Last7Days },
    { label: 'Last 30 Days', value: AnalyticsPeriod.Last30Days },
    { label: 'Last 90 Days', value: AnalyticsPeriod.Last90Days },
    { label: 'This Year', value: AnalyticsPeriod.Year },
    { label: 'Custom Date Range', value: AnalyticsPeriod.Custom },
  ];

  readonly breadcrumbs = [
    { label: 'Dashboard', route: '/dashboard' },
    { label: 'Business Analytics' },
  ];

  ngOnInit(): void {
    this.analyticsState.loadAnalytics();
  }

  onPeriodChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as AnalyticsPeriod;
    if (value === AnalyticsPeriod.Custom) {
      this.showCustomRange.set(true);
      return;
    }
    this.showCustomRange.set(false);
    this.analyticsState.setPeriod(value);
  }

  applyCustomRange(): void {
    const start = this.customStart();
    const end = this.customEnd();
    if (!start || !end) return;
    this.analyticsState.applyCustomRange(start, end);
  }

  refresh(): void {
    this.analyticsState.refresh();
  }

  exportPlaceholder(): void {
    // Placeholder for future PDF export feature.
  }

  retry(): void {
    this.analyticsState.refresh();
  }

  formatMetricValue(card: AnalyticsKpiCard): string | number {
    return card.format === 'currency' ? card.metric.value : card.metric.value;
  }

  growthClass(growth: number): string {
    if (growth > 0) return 'analytics__growth--up';
    if (growth < 0) return 'analytics__growth--down';
    return 'analytics__growth--neutral';
  }

  activityBadgeVariant(type: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Customer: 'info',
      Project: 'primary',
      Task: 'warning',
      Invoice: 'success',
    };
    return map[type] ?? 'neutral';
  }
}
