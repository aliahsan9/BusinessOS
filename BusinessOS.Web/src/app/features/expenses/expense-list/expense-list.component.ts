import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ExpenseService } from '../../../core/services/expense.service';
import { ExpenseCategoryService } from '../../../core/services/expense-category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { ExpenseCategoryDto, ExpenseSummaryDto } from '../../../core/models/expense.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant, ExpenseStatus } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-expense-list',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    CurrencyPipe,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './expense-list.component.html',
  styleUrl: './expense-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpenseListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  readonly ExpenseStatus = ExpenseStatus;
  private readonly expenseService = inject(ExpenseService);
  private readonly categoryService = inject(ExpenseCategoryService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<ExpenseSummaryDto[]>([]);
  readonly categories = signal<ExpenseCategoryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly categoryFilter = signal('');
  readonly statusFilter = signal('');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly sortBy = signal('expenseDate');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly deleteTarget = signal<ExpenseSummaryDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.expense.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.expense.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.expense.delete);
  readonly breadcrumbs = [{ label: 'Expenses', route: ROUTES.expenses.list }, { label: 'Directory' }];
  readonly headerActions = this.canCreate ? [{ label: 'Add Expense', route: ROUTES.expenses.create, icon: '➕' }] : [];

  ngOnInit(): void {
    this.categoryService.getAll().subscribe({ next: (c) => this.categories.set(c) });
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.expenseService
      .getAll({
        page,
        pageSize: this.pageSize(),
        search: this.searchTerm() || undefined,
        categoryId: this.categoryFilter() || undefined,
        status: this.statusFilter() || undefined,
        dateFrom: this.dateFrom() || undefined,
        dateTo: this.dateTo() || undefined,
        sortBy: this.sortBy(),
        sortOrder: this.sortDirection(),
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load expenses.');
          this.loading.set(false);
        },
      });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.load(1);
  }

  onFilterChange(): void {
    this.load(1);
  }

  toggleSort(column: string): void {
    if (this.sortBy() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(column);
      this.sortDirection.set('asc');
    }
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  confirmDelete(expense: ExpenseSummaryDto): void {
    this.deleteTarget.set(expense);
  }

  deleteExpense(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.deleting.set(true);
    this.expenseService.remove(target.id).subscribe({
      next: () => {
        this.notification.success(`Deleted ${target.title}.`);
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete expense.');
        this.deleting.set(false);
      },
    });
  }

  statusVariant(status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Pending: 'warning',
      Approved: 'info',
      Rejected: 'danger',
      Paid: 'success',
    };
    return map[status] ?? 'neutral';
  }

  retry(): void {
    this.load();
  }
}
