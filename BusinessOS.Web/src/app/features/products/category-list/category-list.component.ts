import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CategoryStateService } from '../../../state/category.state';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-category-list',
  standalone: true,
  imports: [
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppPaginationComponent,
    AppButtonComponent,
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
  private readonly categoryState = inject(CategoryStateService);
  private readonly categoryService = inject(CategoryService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = this.categoryState.items;
  readonly page = this.categoryState.page;
  readonly pageSize = this.categoryState.pageSize;
  readonly totalCount = this.categoryState.totalCount;
  readonly totalPages = this.categoryState.totalPages;
  readonly loading = this.categoryState.loading;
  readonly error = this.categoryState.error;

  readonly searchTerm = signal('');
  readonly deleteId = signal<string | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.category.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.category.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.category.delete);

  readonly headerActions = this.canCreate
    ? [{ label: 'Add Category', route: ROUTES.products.categories.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.categoryState.load();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.categoryState.load({ search: term, page: 1 });
  }

  onPageChange(page: number): void {
    this.categoryState.load({ page });
  }

  confirmDelete(id: string): void {
    this.deleteId.set(id);
  }

  deleteCategory(): void {
    const id = this.deleteId();
    if (!id) return;
    this.deleting.set(true);
    this.categoryService.remove(id).subscribe({
      next: () => {
        this.notification.success('Category deleted.');
        this.deleteId.set(null);
        this.deleting.set(false);
        this.categoryState.load();
      },
      error: () => {
        this.notification.error('Failed to delete category. It may have linked products.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.categoryState.load();
  }
}
