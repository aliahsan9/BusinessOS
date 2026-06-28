import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { InventoryStateService } from '../../../state/inventory.state';
import { ProductService } from '../../../core/services/product.service';
import { ProductDto } from '../../../core/models/product.model';
import { ProductHelper } from '../../../core/helpers/product.helper';
import { ExportHelper } from '../../../core/helpers/export.helper';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

interface ReportRow {
  productId: string;
  productName: string;
  sku: string;
  currentStock: number;
  costPrice: number;
  salePrice: number;
  stockValue: number;
  profitPotential: number;
}

@Component({
  selector: 'app-inventory-reports',
  standalone: true,
  imports: [
    CurrencyPipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './inventory-reports.component.html',
  styleUrl: './inventory-reports.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryReportsComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly inventoryState = inject(InventoryStateService);
  private readonly productService = inject(ProductService);

  readonly analytics = this.inventoryState.analytics;
  readonly analyticsLoading = this.inventoryState.analyticsLoadingState;
  readonly reportLoading = signal(true);
  readonly reportError = signal<string | null>(null);
  readonly reportRows = signal<ReportRow[]>([]);

  readonly totalStockValue = computed(() =>
    this.reportRows().reduce((sum, row) => sum + row.stockValue, 0),
  );

  readonly totalProfitPotential = computed(() =>
    this.reportRows().reduce((sum, row) => sum + row.profitPotential, 0),
  );

  readonly breadcrumbs = [
    { label: 'Inventory', route: ROUTES.inventory.overview },
    { label: 'Reports' },
  ];

  readonly headerActions = [
    { label: 'Overview', route: ROUTES.inventory.overview, icon: '🏠', variant: ButtonVariant.Outline },
    { label: 'Export CSV', icon: '📄', action: () => this.exportCsv() },
  ];

  ngOnInit(): void {
    this.inventoryState.loadAnalytics();
    this.loadReportData();
  }

  loadReportData(): void {
    this.reportLoading.set(true);
    this.reportError.set(null);

    this.productService.getAll({ page: 1, pageSize: 500 }).subscribe({
      next: (result) => {
        const rows = result.items.map((product) => this.toReportRow(product));
        this.reportRows.set(rows);
        this.reportLoading.set(false);
      },
      error: () => {
        this.reportError.set('Failed to load report data.');
        this.reportLoading.set(false);
      },
    });
  }

  exportCsv(): void {
    ExportHelper.downloadCsv('inventory-report', this.reportRows(), [
      { header: 'Product', accessor: (r) => r.productName },
      { header: 'SKU', accessor: (r) => r.sku },
      { header: 'Stock', accessor: (r) => r.currentStock },
      { header: 'Cost Price', accessor: (r) => r.costPrice },
      { header: 'Sale Price', accessor: (r) => r.salePrice },
      { header: 'Stock Value', accessor: (r) => r.stockValue },
      { header: 'Profit Potential', accessor: (r) => r.profitPotential },
    ]);
  }

  retry(): void {
    this.inventoryState.loadAnalytics();
    this.loadReportData();
  }

  private toReportRow(product: ProductDto): ReportRow {
    return {
      productId: product.id,
      productName: product.name,
      sku: product.sku,
      currentStock: product.currentStock,
      costPrice: product.costPrice,
      salePrice: product.salePrice,
      stockValue: ProductHelper.stockValue(product.currentStock, product.costPrice),
      profitPotential: ProductHelper.profitPotential(product.currentStock, product.costPrice, product.salePrice),
    };
  }
}
