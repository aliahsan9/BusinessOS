import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { ExpenseCategoryService } from '../../../core/services/expense-category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { ExpenseCategoryDto } from '../../../core/models/expense.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-category-list',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    AppCurrencyPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './category-list.component.html',
  styleUrl: './category-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategoryListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly categoryService = inject(ExpenseCategoryService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<ExpenseCategoryDto[]>([]);
  readonly filteredItems = signal<ExpenseCategoryDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly activeFilter = signal('');
  readonly deleteTarget = signal<ExpenseCategoryDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.expenseCategory.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.expenseCategory.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.expenseCategory.delete);
  readonly breadcrumbs = [{ label: 'Expense Categories', route: ROUTES.expenseCategories.list }, { label: 'Directory' }];
  readonly headerActions = this.canCreate
    ? [{ label: 'Add Category', route: ROUTES.expenseCategories.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.categoryService.getAll().subscribe({
      next: (items) => {
        this.items.set(items);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load expense categories.');
        this.loading.set(false);
      },
    });
  }

  applyFilters(): void {
    let result = this.items();
    const term = this.searchTerm().toLowerCase();
    if (term) {
      result = result.filter(
        (c) => c.name.toLowerCase().includes(term) || (c.description?.toLowerCase().includes(term) ?? false),
      );
    }
    const active = this.activeFilter();
    if (active === 'active') result = result.filter((c) => c.isActive);
    if (active === 'inactive') result = result.filter((c) => !c.isActive);
    this.filteredItems.set(result);
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.applyFilters();
  }

  onFilterChange(): void {
    this.applyFilters();
  }

  confirmDelete(category: ExpenseCategoryDto): void {
    this.deleteTarget.set(category);
  }

  deleteCategory(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.deleting.set(true);
    this.categoryService.remove(target.id).subscribe({
      next: () => {
        this.notification.success(`Deleted ${target.name}.`);
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete category.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
