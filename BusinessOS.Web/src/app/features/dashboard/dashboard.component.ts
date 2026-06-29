import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AppCurrencyPipe } from '../../shared/pipes/app-currency.pipe';
import { DashboardStateService } from '../../state/dashboard.state';
import { ActivityService } from '../../core/services/activity.service';
import { InvoiceService } from '../../core/services/invoice.service';
import { NotificationStateService } from '../../state/notification.state';
import { ActivityDto } from '../../core/models/activity.model';
import { InvoiceSummaryDto } from '../../core/models/invoice.model';
import { ROUTES } from '../../core/constants/route.constants';
import { DashboardPeriod } from '../../core/enums';
import { AppBreadcrumbComponent } from '../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppCardComponent } from '../../shared/components/app-card/app-card.component';
import { AppChartComponent } from '../../shared/components/app-chart/app-chart.component';
import { AppBadgeComponent } from '../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../shared/components/app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    AppCurrencyPipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppCardComponent,
    AppChartComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private readonly dashboardState = inject(DashboardStateService);
  private readonly activityService = inject(ActivityService);
  private readonly invoiceService = inject(InvoiceService);
  private readonly notificationState = inject(NotificationStateService);

  readonly recentActivities = signal<ActivityDto[]>([]);
  readonly recentLogins = signal<ActivityDto[]>([]);
  readonly latestInvoices = signal<InvoiceSummaryDto[]>([]);
  readonly activityLoading = signal(false);
  readonly widgetsLoading = signal(false);
  readonly routes = ROUTES;
  readonly unreadCount = this.notificationState.unreadCount;

  readonly overview = this.dashboardState.overview;
  readonly sales = this.dashboardState.sales;
  readonly customers = this.dashboardState.customers;
  readonly products = this.dashboardState.products;
  readonly inventory = this.dashboardState.inventory;
  readonly orders = this.dashboardState.orders;
  readonly revenueChart = this.dashboardState.revenueChart;
  readonly ordersChart = this.dashboardState.ordersChart;
  readonly loading = this.dashboardState.loading;
  readonly error = this.dashboardState.error;
  readonly period = this.dashboardState.period;

  readonly periods = [
    { label: 'Today', value: DashboardPeriod.Today },
    { label: 'Week', value: DashboardPeriod.Week },
    { label: 'Month', value: DashboardPeriod.Month },
    { label: 'Year', value: DashboardPeriod.Year },
    { label: 'All', value: DashboardPeriod.All },
  ];

  readonly breadcrumbs = [{ label: 'Dashboard', route: '/dashboard' }, { label: 'Overview' }];

  ngOnInit(): void {
    this.dashboardState.loadDashboard();
    this.loadRecentActivity();
    this.loadWidgets();
  }

  loadWidgets(): void {
    this.widgetsLoading.set(true);
    void this.notificationState.refresh(5);

    this.activityService.getAll({ page: 1, pageSize: 5, action: 'Login' }).subscribe({
      next: (result) => {
        this.recentLogins.set(result.items);
      },
      error: () => this.recentLogins.set([]),
    });

    this.invoiceService.getAll({ page: 1, pageSize: 5 }).subscribe({
      next: (result) => {
        this.latestInvoices.set(result.items);
        this.widgetsLoading.set(false);
      },
      error: () => {
        this.latestInvoices.set([]);
        this.widgetsLoading.set(false);
      },
    });
  }

  loadRecentActivity(): void {
    this.activityLoading.set(true);
    this.activityService.getRecent(10).subscribe({
      next: (items) => {
        this.recentActivities.set(items);
        this.activityLoading.set(false);
      },
      error: () => {
        this.recentActivities.set([]);
        this.activityLoading.set(false);
      },
    });
  }

  onPeriodChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as DashboardPeriod;
    this.dashboardState.setPeriod(value);
  }

  retry(): void {
    this.dashboardState.loadDashboard(this.period());
  }

  getStatusVariant(status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Pending: 'warning',
      Confirmed: 'info',
      Processing: 'primary',
      Completed: 'success',
      Cancelled: 'danger',
    };
    return map[status] ?? 'neutral';
  }
}
