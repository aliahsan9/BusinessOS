import { ButtonVariant } from '../../../core/enums';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ProductService } from '../../../core/services/product.service';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CategoryDto } from '../../../core/models/category.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ProductHelper } from '../../../core/helpers/product.helper';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CurrencyPipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppInputComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
  ],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productService = inject(ProductService);
  private readonly categoryService = inject(CategoryService);
  private readonly notification = inject(NotificationService);

  readonly categories = signal<CategoryDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly productId = signal<string | null>(null);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    categoryId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    sku: ['', [Validators.required, Validators.maxLength(50)]],
    description: ['', Validators.maxLength(2000)],
    costPrice: [0, [Validators.required, Validators.min(0.01)]],
    salePrice: [0, [Validators.required, Validators.min(0.01)]],
    reorderLevel: [0, [Validators.required, Validators.min(0)]],
    isActive: [true],
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.productId.set(id);

    this.categoryService.getAllForSelect().subscribe({
      next: (cats) => this.categories.set(cats),
    });

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.productService.getById(id).subscribe({
        next: (product) => {
          this.form.patchValue({
            categoryId: product.categoryId,
            name: product.name,
            sku: product.sku,
            description: product.description ?? '',
            costPrice: product.costPrice,
            salePrice: product.salePrice,
            reorderLevel: product.reorderLevel,
            isActive: product.isActive,
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load product.');
          this.loading.set(false);
        },
      });
    }
  }

  margin(): number {
    return ProductHelper.profitMargin(this.form.controls.costPrice.value, this.form.controls.salePrice.value);
  }

  fieldError(field: string): string | null {
    return getFieldError(this.form.get(field), field);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();

    if (this.isEdit() && this.productId()) {
      this.productService.update(this.productId()!, value).subscribe({
        next: () => {
          this.notification.success('Product updated.');
          this.router.navigate([ROUTES.products.base, this.productId()]);
        },
        error: () => {
          this.notification.error('Failed to update product.');
          this.saving.set(false);
        },
      });
    } else {
      this.productService.create(value).subscribe({
        next: (res) => {
          this.notification.success('Product created.');
          this.router.navigate([ROUTES.products.base, res.id]);
        },
        error: () => {
          this.notification.error('Failed to create product.');
          this.saving.set(false);
        },
      });
    }
  }
}
