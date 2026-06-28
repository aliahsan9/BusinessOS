import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductStateService } from '../../../state/product.state';
import { CategoryService } from '../../../core/services/category.service';
import { ProductService } from '../../../core/services/product.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { CategoryDto } from '../../../core/models/category.model';
import { ProductDto } from '../../../core/models/product.model';
import { ProductHelper } from '../../../core/helpers/product.helper';
import { ExportHelper } from '../../../core/helpers/export.helper';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
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
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    DecimalPipe,
    FormsModule,
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
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly productState = inject(ProductStateService);
  private readonly categoryService = inject(CategoryService);
  private readonly productService = inject(ProductService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = this.productState.items;
  readonly page = this.productState.page;
  readonly pageSize = this.productState.pageSize;
  readonly totalCount = this.productState.totalCount;
  readonly totalPages = this.productState.totalPages;
  readonly loading = this.productState.loading;
  readonly error = this.productState.error;

  readonly categories = signal<CategoryDto[]>([]);
  readonly selectedIds = signal<Set<string>>(new Set());
  readonly showDeleteConfirm = signal(false);
  readonly showBulkUpdate = signal(false);
  readonly bulkLoading = signal(false);
  readonly searchTerm = signal('');
  readonly categoryFilter = signal('');
  readonly statusFilter = signal('');
  readonly bulkCategoryIdValue = '';
  bulkIsActiveValue: boolean | null = null;

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.product.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.product.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.product.delete);

  readonly breadcrumbs = [
    { label: 'Products', route: ROUTES.products.list },
    { label: 'Catalog' },
  ];

  readonly headerActions = [
    { label: 'Categories', route: ROUTES.products.categories.list, icon: '🏷️', variant: ButtonVariant.Outline },
    ...(this.canCreate ? [{ label: 'Add Product', route: ROUTES.products.create, icon: '➕' }] : []),
  ];

  ngOnInit(): void {
    this.productState.load();
    this.categoryService.getAllForSelect().subscribe({
      next: (cats) => this.categories.set(cats),
    });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.productState.load({ search: term, page: 1 });
  }

  onCategoryFilter(categoryId: string): void {
    this.categoryFilter.set(categoryId);
    this.productState.load({ categoryId: categoryId || undefined, page: 1 });
  }

  onStatusFilter(status: string): void {
    this.statusFilter.set(status);
    this.productState.load({ page: 1 });
  }

  onSort(column: string): void {
    const current = this.productState.items();
    void current;
    this.productState.load({ sortBy: column, page: 1 });
  }

  onPageChange(page: number): void {
    this.productState.load({ page });
  }

  toggleSelect(id: string, checked: boolean): void {
    this.selectedIds.update((set) => {
      const next = new Set(set);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.selectedIds.set(new Set(this.items().map((p) => p.id)));
    } else {
      this.selectedIds.set(new Set());
    }
  }

  profitMargin(product: ProductDto): number {
    return ProductHelper.profitMargin(product.costPrice, product.salePrice);
  }

  exportCsv(): void {
    ExportHelper.downloadCsv('products', this.items(), [
      { header: 'Name', accessor: (p) => p.name },
      { header: 'SKU', accessor: (p) => p.sku },
      { header: 'Cost Price', accessor: (p) => p.costPrice },
      { header: 'Sale Price', accessor: (p) => p.salePrice },
      { header: 'Stock', accessor: (p) => p.currentStock },
      { header: 'Reorder Level', accessor: (p) => p.reorderLevel },
      { header: 'Active', accessor: (p) => p.isActive },
    ]);
  }

  exportExcel(): void {
    ExportHelper.downloadExcel('products', this.items(), [
      { header: 'Name', accessor: (p) => p.name },
      { header: 'SKU', accessor: (p) => p.sku },
      { header: 'Cost Price', accessor: (p) => p.costPrice },
      { header: 'Sale Price', accessor: (p) => p.salePrice },
      { header: 'Stock', accessor: (p) => p.currentStock },
      { header: 'Reorder Level', accessor: (p) => p.reorderLevel },
      { header: 'Active', accessor: (p) => p.isActive },
    ]);
  }

  confirmBulkDelete(): void {
    if (this.selectedIds().size === 0) return;
    this.showDeleteConfirm.set(true);
  }

  bulkDelete(): void {
    const ids = [...this.selectedIds()];
    this.bulkLoading.set(true);
    this.productService.bulkDelete(ids).subscribe({
      next: () => {
        this.notification.success(`Deleted ${ids.length} product(s).`);
        this.selectedIds.set(new Set());
        this.showDeleteConfirm.set(false);
        this.bulkLoading.set(false);
        this.productState.load();
      },
      error: () => {
        this.notification.error('Failed to delete products.');
        this.bulkLoading.set(false);
      },
    });
  }

  applyBulkUpdate(): void {
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;
    this.bulkLoading.set(true);
    this.productService
      .bulkUpdate(ids, {
        categoryId: this.bulkCategoryIdValue || undefined,
        isActive: this.bulkIsActiveValue ?? undefined,
      })
      .subscribe({
        next: () => {
          this.notification.success(`Updated ${ids.length} product(s).`);
          this.showBulkUpdate.set(false);
          this.bulkLoading.set(false);
          this.selectedIds.set(new Set());
          this.productState.load();
        },
        error: () => {
          this.notification.error('Failed to update products.');
          this.bulkLoading.set(false);
        },
      });
  }

  retry(): void {
    this.productState.load();
  }

  getCategoryName(categoryId: string): string {
    return this.categories().find((c) => c.id === categoryId)?.name ?? '—';
  }

  filteredItems(): ProductDto[] {
    const status = this.statusFilter();
    if (!status) return this.items();
    if (status === 'active') return this.items().filter((p) => p.isActive);
    if (status === 'inactive') return this.items().filter((p) => !p.isActive);
    if (status === 'low') return this.items().filter((p) => p.currentStock > 0 && p.currentStock <= p.reorderLevel);
    if (status === 'out') return this.items().filter((p) => p.currentStock <= 0);
    return this.items();
  }
}
