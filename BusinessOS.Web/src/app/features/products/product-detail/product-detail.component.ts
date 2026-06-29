import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { ProductService } from '../../../core/services/product.service';
import { CategoryService } from '../../../core/services/category.service';
import { InventoryService } from '../../../core/services/inventory.service';
import { ProductDto } from '../../../core/models/product.model';
import { InventoryDetail } from '../../../core/models/inventory.model';
import { ProductHelper } from '../../../core/helpers/product.helper';
import { ROUTES } from '../../../core/constants/route.constants';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [
    RouterLink,
    AppCurrencyPipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly productService = inject(ProductService);
  private readonly categoryService = inject(CategoryService);
  private readonly inventoryService = inject(InventoryService);
  private readonly tokenService = inject(TokenService);

  readonly product = signal<ProductDto | null>(null);
  readonly inventory = signal<InventoryDetail | null>(null);
  readonly categoryName = signal('—');
  readonly loading = signal(true);
  readonly routes = ROUTES;
  readonly canEdit = this.tokenService.hasPermission(PermissionCodes.product.update);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.productService.getById(id).subscribe({
      next: (p) => {
        this.product.set(p);
        this.loading.set(false);
        this.categoryService.getById(p.categoryId).subscribe({
          next: (cat) => this.categoryName.set(cat.name),
        });
        this.inventoryService.getByProductId(id).subscribe({
          next: (inv) => this.inventory.set(inv),
        });
      },
      error: () => this.loading.set(false),
    });
  }

  margin(): number {
    const p = this.product();
    return p ? ProductHelper.profitMargin(p.costPrice, p.salePrice) : 0;
  }

  stockValue(): number {
    const p = this.product();
    return p ? ProductHelper.stockValue(p.currentStock, p.costPrice) : 0;
  }

  headerActions() {
    return this.canEdit
      ? [{ label: 'Edit', route: `${ROUTES.products.base}/${this.product()?.id}/edit`, icon: '✏️', variant: ButtonVariant.Primary }]
      : [];
  }
}
