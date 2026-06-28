import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { SupplierService } from '../../../core/services/supplier.service';
import { SupplierDto, SupplierProductSummary, SupplierPurchaseSummary } from '../../../core/models/supplier.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-supplier-detail',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppPaginationComponent,
  ],
  templateUrl: './supplier-detail.component.html',
  styleUrl: './supplier-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupplierDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly supplierService = inject(SupplierService);
  private readonly tokenService = inject(TokenService);

  readonly supplier = signal<SupplierDto | null>(null);
  readonly purchases = signal<SupplierPurchaseSummary[]>([]);
  readonly products = signal<SupplierProductSummary[]>([]);
  readonly purchasePage = signal(1);
  readonly purchaseTotalCount = signal(0);
  readonly purchaseTotalPages = signal(0);
  readonly loading = signal(true);
  readonly purchasesLoading = signal(false);
  readonly productsLoading = signal(false);
  readonly routes = ROUTES;
  readonly canEdit = this.tokenService.hasPermission(PermissionCodes.supplier.update);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.supplierService.getById(id).subscribe({
      next: (s) => {
        this.supplier.set(s);
        this.loading.set(false);
        this.loadPurchases(id);
        this.loadProducts(id);
      },
      error: () => this.loading.set(false),
    });
  }

  loadPurchases(supplierId: string, page = 1): void {
    this.purchasesLoading.set(true);
    this.supplierService.getPurchaseHistory(supplierId, { page, pageSize: 5 }).subscribe({
      next: (result) => {
        this.purchases.set(result.items);
        this.purchasePage.set(result.page);
        this.purchaseTotalCount.set(result.totalCount);
        this.purchaseTotalPages.set(result.totalPages);
        this.purchasesLoading.set(false);
      },
      error: () => this.purchasesLoading.set(false),
    });
  }

  loadProducts(supplierId: string): void {
    this.productsLoading.set(true);
    this.supplierService.getProducts(supplierId).subscribe({
      next: (items) => {
        this.products.set(items);
        this.productsLoading.set(false);
      },
      error: () => this.productsLoading.set(false),
    });
  }

  onPurchasePageChange(page: number): void {
    const id = this.supplier()?.id;
    if (id) this.loadPurchases(id, page);
  }

  headerActions() {
    return this.canEdit
      ? [
          {
            label: 'Edit',
            route: `${ROUTES.suppliers.base}/${this.supplier()?.id}/edit`,
            icon: '✏️',
            variant: ButtonVariant.Primary,
          },
        ]
      : [];
  }

  purchaseStatusVariant(status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Draft: 'neutral',
      Pending: 'warning',
      Approved: 'info',
      Received: 'success',
      Cancelled: 'danger',
    };
    return map[status] ?? 'neutral';
  }
}
