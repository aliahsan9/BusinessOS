import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { CustomerService } from '../../../core/services/customer.service';
import { OrderService } from '../../../core/services/order.service';
import { InvoiceService } from '../../../core/services/invoice.service';
import { PaymentService } from '../../../core/services/payment.service';
import { DashboardService } from '../../../core/services/dashboard.service';
import { ExpenseService } from '../../../core/services/expense.service';
import { SupplierService } from '../../../core/services/supplier.service';
import { InventoryService } from '../../../core/services/inventory.service';
import { AuditService } from '../../../core/services/audit.service';
import { UserService } from '../../../core/services/user.service';
import { CustomerSummaryDto } from '../../../core/models/customer.model';
import { OrderSummaryDto } from '../../../core/models/order.model';
import { InvoiceSummaryDto } from '../../../core/models/invoice.model';
import { PaymentSummaryDto } from '../../../core/models/payment.model';
import { ExpenseSummaryDto } from '../../../core/models/expense.model';
import { SupplierDto } from '../../../core/models/supplier.model';
import { InventorySummary } from '../../../core/models/inventory.model';
import { AuditLogDto } from '../../../core/models/audit.model';
import { UserSummaryDto } from '../../../core/models/user.model';
import { RevenueTrend, SalesAnalyticsResponse } from '../../../core/models/dashboard.model';
import { ButtonVariant, InvoiceStatus } from '../../../core/enums';
import { ExportColumn, ExportHelper } from '../../../core/helpers/export.helper';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

type ReportTab =
  | 'customers'
  | 'sales'
  | 'invoices'
  | 'payments'
  | 'revenue'
  | 'outstanding'
  | 'expenses'
  | 'suppliers'
  | 'inventory'
  | 'audit'
  | 'users';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    AppCurrencyPipe,
    DatePipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly customerService = inject(CustomerService);
  private readonly orderService = inject(OrderService);
  private readonly invoiceService = inject(InvoiceService);
  private readonly paymentService = inject(PaymentService);
  private readonly dashboardService = inject(DashboardService);
  private readonly expenseService = inject(ExpenseService);
  private readonly supplierService = inject(SupplierService);
  private readonly inventoryService = inject(InventoryService);
  private readonly auditService = inject(AuditService);
  private readonly userService = inject(UserService);

  readonly activeTab = signal<ReportTab>('customers');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly customers = signal<CustomerSummaryDto[]>([]);
  readonly orders = signal<OrderSummaryDto[]>([]);
  readonly invoices = signal<InvoiceSummaryDto[]>([]);
  readonly payments = signal<PaymentSummaryDto[]>([]);
  readonly sales = signal<SalesAnalyticsResponse | null>(null);
  readonly revenueTrends = signal<RevenueTrend[]>([]);
  readonly outstanding = signal<InvoiceSummaryDto[]>([]);
  readonly expenses = signal<ExpenseSummaryDto[]>([]);
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly inventory = signal<InventorySummary[]>([]);
  readonly auditLogs = signal<AuditLogDto[]>([]);
  readonly users = signal<UserSummaryDto[]>([]);

  readonly routes = ROUTES;
  readonly tabs: { id: ReportTab; label: string }[] = [
    { id: 'customers', label: 'Customers' },
    { id: 'sales', label: 'Sales' },
    { id: 'invoices', label: 'Invoices' },
    { id: 'payments', label: 'Payments' },
    { id: 'revenue', label: 'Revenue' },
    { id: 'outstanding', label: 'Outstanding' },
    { id: 'expenses', label: 'Expenses' },
    { id: 'suppliers', label: 'Suppliers' },
    { id: 'inventory', label: 'Inventory' },
    { id: 'audit', label: 'Audit' },
    { id: 'users', label: 'Users' },
  ];

  readonly breadcrumbs = [{ label: 'Reports', route: ROUTES.reports }];

  ngOnInit(): void {
    this.loadTab('customers');
  }

  selectTab(tab: ReportTab): void {
    this.activeTab.set(tab);
    this.loadTab(tab);
  }

  loadTab(tab: ReportTab): void {
    this.loading.set(true);
    this.error.set(null);

    switch (tab) {
      case 'customers':
        this.customerService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.customers.set(r.items)),
          error: () => this.failLoad('Failed to load customer report.'),
        });
        break;
      case 'sales':
        this.orderService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.orders.set(r.items)),
          error: () => this.failLoad('Failed to load sales report.'),
        });
        break;
      case 'invoices':
        this.invoiceService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.invoices.set(r.items)),
          error: () => this.failLoad('Failed to load invoice report.'),
        });
        break;
      case 'payments':
        this.paymentService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.payments.set(r.items)),
          error: () => this.failLoad('Failed to load payment report.'),
        });
        break;
      case 'revenue':
        this.dashboardService.getSales().subscribe({
          next: (r) =>
            this.finishLoad(() => {
              this.sales.set(r);
              this.revenueTrends.set(r.revenueTrends);
            }),
          error: () => this.failLoad('Failed to load revenue report.'),
        });
        break;
      case 'outstanding':
        this.invoiceService.getAll({ pageSize: 500 }).subscribe({
          next: (r) =>
            this.finishLoad(() => this.outstanding.set(r.items.filter((i) => i.outstandingAmount > 0))),
          error: () => this.failLoad('Failed to load outstanding report.'),
        });
        break;
      case 'expenses':
        this.expenseService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.expenses.set(r.items)),
          error: () => this.failLoad('Failed to load expense report.'),
        });
        break;
      case 'suppliers':
        this.supplierService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.suppliers.set(r.items)),
          error: () => this.failLoad('Failed to load supplier report.'),
        });
        break;
      case 'inventory':
        this.inventoryService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.inventory.set(r.items)),
          error: () => this.failLoad('Failed to load inventory report.'),
        });
        break;
      case 'audit':
        this.auditService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.auditLogs.set(r.items)),
          error: () => this.failLoad('Failed to load audit report.'),
        });
        break;
      case 'users':
        this.userService.getAll({ pageSize: 500 }).subscribe({
          next: (r) => this.finishLoad(() => this.users.set(r.items)),
          error: () => this.failLoad('Failed to load user report.'),
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

  printReport(): void {
    window.print();
  }

  private export(format: 'csv' | 'excel'): void {
    const tab = this.activeTab();
    const filename = `report-${tab}-${new Date().toISOString().slice(0, 10)}`;

    if (tab === 'customers') {
      const columns: ExportColumn<CustomerSummaryDto>[] = [
        { header: 'Name', accessor: (r) => r.fullName },
        { header: 'Email', accessor: (r) => r.email },
        { header: 'Phone', accessor: (r) => r.phoneNumber },
        { header: 'City', accessor: (r) => r.city },
      ];
      this.download(format, filename, this.customers(), columns);
    } else if (tab === 'sales') {
      const columns: ExportColumn<OrderSummaryDto>[] = [
        { header: 'Order #', accessor: (r) => r.orderNumber },
        { header: 'Customer', accessor: (r) => r.customerName },
        { header: 'Date', accessor: (r) => r.orderDate },
        { header: 'Status', accessor: (r) => r.status },
        { header: 'Total', accessor: (r) => r.grandTotal },
      ];
      this.download(format, filename, this.orders(), columns);
    } else if (tab === 'invoices') {
      const columns: ExportColumn<InvoiceSummaryDto>[] = [
        { header: 'Invoice #', accessor: (r) => r.invoiceNumber },
        { header: 'Customer', accessor: (r) => r.customerName },
        { header: 'Order', accessor: (r) => r.orderNumber },
        { header: 'Status', accessor: (r) => r.status },
        { header: 'Total', accessor: (r) => r.grandTotal },
        { header: 'Outstanding', accessor: (r) => r.outstandingAmount },
      ];
      this.download(format, filename, this.invoices(), columns);
    } else if (tab === 'payments') {
      const columns: ExportColumn<PaymentSummaryDto>[] = [
        { header: 'Date', accessor: (r) => r.paymentDate },
        { header: 'Customer', accessor: (r) => r.customerName },
        { header: 'Order', accessor: (r) => r.orderNumber },
        { header: 'Method', accessor: (r) => r.paymentMethod },
        { header: 'Amount', accessor: (r) => r.amount },
        { header: 'Reference', accessor: (r) => r.referenceNo },
      ];
      this.download(format, filename, this.payments(), columns);
    } else if (tab === 'revenue') {
      const columns: ExportColumn<RevenueTrend>[] = [
        { header: 'Date', accessor: (r) => r.date },
        { header: 'Revenue', accessor: (r) => r.revenue },
        { header: 'Orders', accessor: (r) => r.orderCount },
      ];
      this.download(format, filename, this.revenueTrends(), columns);
    } else if (tab === 'outstanding') {
      const columns: ExportColumn<InvoiceSummaryDto>[] = [
        { header: 'Invoice #', accessor: (r) => r.invoiceNumber },
        { header: 'Customer', accessor: (r) => r.customerName },
        { header: 'Due Date', accessor: (r) => r.dueDate },
        { header: 'Status', accessor: (r) => r.status },
        { header: 'Total', accessor: (r) => r.grandTotal },
        { header: 'Outstanding', accessor: (r) => r.outstandingAmount },
      ];
      this.download(format, filename, this.outstanding(), columns);
    } else if (tab === 'expenses') {
      const columns: ExportColumn<ExpenseSummaryDto>[] = [
        { header: 'Title', accessor: (r) => r.title },
        { header: 'Category', accessor: (r) => r.categoryName },
        { header: 'Date', accessor: (r) => r.expenseDate },
        { header: 'Amount', accessor: (r) => r.amount },
        { header: 'Status', accessor: (r) => r.status },
      ];
      this.download(format, filename, this.expenses(), columns);
    } else if (tab === 'suppliers') {
      const columns: ExportColumn<SupplierDto>[] = [
        { header: 'Name', accessor: (r) => r.name },
        { header: 'Email', accessor: (r) => r.email },
        { header: 'Phone', accessor: (r) => r.phone },
        { header: 'Contact', accessor: (r) => r.contactPerson ?? '' },
      ];
      this.download(format, filename, this.suppliers(), columns);
    } else if (tab === 'inventory') {
      const columns: ExportColumn<InventorySummary>[] = [
        { header: 'Product', accessor: (r) => r.productName },
        { header: 'SKU', accessor: (r) => r.productSku },
        { header: 'Stock', accessor: (r) => r.currentStock },
        { header: 'Reorder Level', accessor: (r) => r.reorderLevel },
        { header: 'Low Stock', accessor: (r) => (r.isLowStock ? 'Yes' : 'No') },
      ];
      this.download(format, filename, this.inventory(), columns);
    } else if (tab === 'audit') {
      const columns: ExportColumn<AuditLogDto>[] = [
        { header: 'Timestamp', accessor: (r) => r.createdAt },
        { header: 'Action', accessor: (r) => r.action },
        { header: 'Entity Type', accessor: (r) => r.entityType },
        { header: 'Entity ID', accessor: (r) => r.entityId },
        { header: 'User ID', accessor: (r) => r.actorUserId },
      ];
      this.download(format, filename, this.auditLogs(), columns);
    } else if (tab === 'users') {
      const columns: ExportColumn<UserSummaryDto>[] = [
        { header: 'Name', accessor: (r) => r.fullName },
        { header: 'Email', accessor: (r) => r.email },
        { header: 'Roles', accessor: (r) => r.roles.join(', ') },
        { header: 'Active', accessor: (r) => (r.isActive ? 'Yes' : 'No') },
      ];
      this.download(format, filename, this.users(), columns);
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

  isOverdueStatus(status: InvoiceStatus): boolean {
    return status === InvoiceStatus.Overdue;
  }
}
