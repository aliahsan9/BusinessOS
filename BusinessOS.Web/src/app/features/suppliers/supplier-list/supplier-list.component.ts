import { ButtonVariant } from '../../../core/enums';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { SupplierDto } from '../../../core/models/supplier.model';
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

@Component({
  selector: 'app-supplier-list',
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
  templateUrl: './supplier-list.component.html',
  styleUrl: './supplier-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupplierListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly supplierService = inject(SupplierService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<SupplierDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly deleteTarget = signal<SupplierDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.supplier.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.supplier.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.supplier.delete);

  readonly breadcrumbs = [{ label: 'Suppliers', route: ROUTES.suppliers.list }, { label: 'Directory' }];

  readonly headerActions = this.canCreate
    ? [{ label: 'Add Supplier', route: ROUTES.suppliers.create, icon: '➕' }]
    : [];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.supplierService
      .getAll({ page, pageSize: this.pageSize(), search: this.searchTerm() || undefined })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load suppliers.');
          this.loading.set(false);
        },
      });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  confirmDelete(supplier: SupplierDto): void {
    this.deleteTarget.set(supplier);
  }

  deleteSupplier(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.supplierService.remove(target.id).subscribe({
      next: () => {
        this.notification.success(`Deleted ${target.name}.`);
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete supplier.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
