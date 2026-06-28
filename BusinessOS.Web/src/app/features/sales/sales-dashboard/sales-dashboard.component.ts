import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { DashboardService } from '../../../core/services/dashboard.service';
import { InvoiceService } from '../../../core/services/invoice.service';
import {
  ChartDataResponse,
  DashboardOverviewResponse,
  OrderAnalyticsResponse,
  ProductAnalyticsResponse,
  SalesAnalyticsResponse,
} from '../../../core/models/dashboard.model';
import { InvoiceStatus, OrderStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppChartComponent } from '../../../shared/components/app-chart/app-chart.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

interface InvoiceStats {
  total: number;
  paid: number;
  outstanding: number;
  overdue: number;
  totalOutstandingAmount: number;
}

@Component({
  selector: 'app-sales-dashboard',
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
  templateUrl: './sales-dashboard.component.html',
  styleUrl: './sales-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SalesDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly invoiceService = inject(InvoiceService);

  readonly overview = signal<DashboardOverviewResponse | null>(null);
  readonly sales = signal<SalesAnalyticsResponse | null>(null);
  readonly orders = signal<OrderAnalyticsResponse | null>(null);
  readonly products = signal<ProductAnalyticsResponse | null>(null);
  readonly invoiceStats = signal<InvoiceStats | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly routes = ROUTES;
  readonly breadcrumbs = [{ label: 'Sales', route: ROUTES.sales.dashboard }, { label: 'Dashboard' }];

  readonly pendingOrdersCount = computed(() => {
    const statuses = this.orders()?.ordersByStatus ?? [];
    const pending = statuses.find((s) => s.status === OrderStatus.Pending);
    return pending?.count ?? 0;
  });

  readonly topProductsChart = computed<ChartDataResponse | null>(() => {
    const best = this.products()?.bestSellingProducts ?? [];
    if (!best.length) return null;
    return {
      chartType: 'bar',
      title: 'Top Products',
      labels: best.slice(0, 10).map((p) => p.productName),
      datasets: [
        {
          label: 'Revenue',
          data: best.slice(0, 10).map((p) => p.totalRevenue),
          chartStyle: 'bar',
        },
      ],
      dateRange: this.products()!.dateRange,
    };
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      overview: this.dashboardService.getOverview(),
      sales: this.dashboardService.getSales(),
      orders: this.dashboardService.getOrders(),
      products: this.dashboardService.getProducts({ top: 10 }),
      invoices: this.invoiceService.getAll({ pageSize: 500 }),
    }).subscribe({
      next: ({ overview, sales, orders, products, invoices }) => {
        this.overview.set(overview);
        this.sales.set(sales);
        this.orders.set(orders);
        this.products.set(products);
        this.invoiceStats.set(this.computeInvoiceStats(invoices.items));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load sales dashboard.');
        this.loading.set(false);
      },
    });
  }

  private computeInvoiceStats(
    items: { status: InvoiceStatus; outstandingAmount: number }[],
  ): InvoiceStats {
    return {
      total: items.length,
      paid: items.filter((i) => i.status === InvoiceStatus.Paid).length,
      outstanding: items.filter((i) => i.outstandingAmount > 0).length,
      overdue: items.filter((i) => i.status === InvoiceStatus.Overdue).length,
      totalOutstandingAmount: items.reduce((sum, i) => sum + i.outstandingAmount, 0),
    };
  }

  retry(): void {
    this.load();
  }
}
