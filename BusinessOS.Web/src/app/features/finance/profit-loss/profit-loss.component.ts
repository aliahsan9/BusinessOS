import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FinanceService } from '../../../core/services/finance.service';
import {
  ExpenseBreakdown,
  ExpenseCategoryItem,
  ProfitLossReport,
  RevenueBreakdown,
  RevenueCategoryItem,
  TrendPoint,
} from '../../../core/models/finance.model';
import { ButtonVariant } from '../../../core/enums';
import { ExportColumn, ExportHelper } from '../../../core/helpers/export.helper';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

type ProfitLossTab = 'summary' | 'monthly' | 'yearly' | 'revenue' | 'expenses';

@Component({
  selector: 'app-profit-loss',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './profit-loss.component.html',
  styleUrl: './profit-loss.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfitLossComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly financeService = inject(FinanceService);

  readonly activeTab = signal<ProfitLossTab>('summary');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly profitLoss = signal<ProfitLossReport | null>(null);
  readonly monthlyReport = signal<ProfitLossReport | null>(null);
  readonly yearlyReport = signal<ProfitLossReport | null>(null);
  readonly revenueBreakdown = signal<RevenueBreakdown | null>(null);
  readonly expenseBreakdown = signal<ExpenseBreakdown | null>(null);

  readonly routes = ROUTES;
  readonly tabs: { id: ProfitLossTab; label: string }[] = [
    { id: 'summary', label: 'Summary' },
    { id: 'monthly', label: 'Monthly' },
    { id: 'yearly', label: 'Yearly' },
    { id: 'revenue', label: 'Revenue Breakdown' },
    { id: 'expenses', label: 'Expense Breakdown' },
  ];
  readonly breadcrumbs = [
    { label: 'Finance', route: ROUTES.finance.dashboard },
    { label: 'Profit & Loss' },
  ];

  ngOnInit(): void {
    this.loadTab('summary');
  }

  selectTab(tab: ProfitLossTab): void {
    this.activeTab.set(tab);
    this.loadTab(tab);
  }

  loadTab(tab: ProfitLossTab): void {
    this.loading.set(true);
    this.error.set(null);

    switch (tab) {
      case 'summary':
        this.financeService.getProfitLoss().subscribe({
          next: (r) => this.finishLoad(() => this.profitLoss.set(r)),
          error: () => this.failLoad('Failed to load profit & loss summary.'),
        });
        break;
      case 'monthly':
        this.financeService.getProfitLoss({ period: 'monthly', groupBy: 'month' }).subscribe({
          next: (r) => this.finishLoad(() => this.monthlyReport.set(r)),
          error: () => this.failLoad('Failed to load monthly report.'),
        });
        break;
      case 'yearly':
        this.financeService.getProfitLoss({ period: 'yearly', groupBy: 'year' }).subscribe({
          next: (r) => this.finishLoad(() => this.yearlyReport.set(r)),
          error: () => this.failLoad('Failed to load yearly report.'),
        });
        break;
      case 'revenue':
        this.financeService.getRevenueBreakdown().subscribe({
          next: (r) => this.finishLoad(() => this.revenueBreakdown.set(r)),
          error: () => this.failLoad('Failed to load revenue breakdown.'),
        });
        break;
      case 'expenses':
        this.financeService.getExpenseBreakdown().subscribe({
          next: (r) => this.finishLoad(() => this.expenseBreakdown.set(r)),
          error: () => this.failLoad('Failed to load expense breakdown.'),
        });
        break;
    }
  }

  exportCsv(): void {
    this.export('csv');
  }

  exportExcel(): void {
    this.export('excel');
  }

  private export(format: 'csv' | 'excel'): void {
    const tab = this.activeTab();
    const filename = `profit-loss-${tab}-${new Date().toISOString().slice(0, 10)}`;

    if (tab === 'summary' || tab === 'monthly' || tab === 'yearly') {
      const report =
        tab === 'summary' ? this.profitLoss() : tab === 'monthly' ? this.monthlyReport() : this.yearlyReport();
      const rows = report?.periodBreakdown ?? [];
      const columns: ExportColumn<TrendPoint>[] = [
        { header: 'Period', accessor: (r) => r.period },
        { header: 'Amount', accessor: (r) => r.amount },
        { header: 'Count', accessor: (r) => r.count },
      ];
      this.download(format, filename, rows, columns);
    } else if (tab === 'revenue') {
      const rows = this.revenueBreakdown()?.byPaymentMethod ?? [];
      const columns: ExportColumn<RevenueCategoryItem>[] = [
        { header: 'Category', accessor: (r) => r.category },
        { header: 'Amount', accessor: (r) => r.amount },
        { header: 'Count', accessor: (r) => r.count },
      ];
      this.download(format, filename, rows, columns);
    } else if (tab === 'expenses') {
      const rows = this.expenseBreakdown()?.byCategory ?? [];
      const columns: ExportColumn<ExpenseCategoryItem>[] = [
        { header: 'Category', accessor: (r) => r.categoryName },
        { header: 'Amount', accessor: (r) => r.amount },
        { header: 'Count', accessor: (r) => r.count },
        { header: 'Percentage', accessor: (r) => r.percentage },
      ];
      this.download(format, filename, rows, columns);
    }
  }

  private download<T>(format: 'csv' | 'excel', filename: string, rows: T[], columns: ExportColumn<T>[]): void {
    if (format === 'csv') {
      ExportHelper.downloadCsv(filename, rows, columns);
    } else {
      ExportHelper.downloadExcel(filename, rows, columns);
    }
  }

  private finishLoad(setter: () => void): void {
    setter();
    this.loading.set(false);
  }

  private failLoad(message: string): void {
    this.error.set(message);
    this.loading.set(false);
  }
}
