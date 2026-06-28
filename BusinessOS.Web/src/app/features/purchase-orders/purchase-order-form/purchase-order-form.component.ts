import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { SupplierService } from '../../../core/services/supplier.service';
import { ProductService } from '../../../core/services/product.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SupplierDto } from '../../../core/models/supplier.model';
import { ProductDto } from '../../../core/models/product.model';
import { ButtonVariant,  PurchaseOrderStatus  } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-purchase-order-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CurrencyPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppInputComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
  ],
  templateUrl: './purchase-order-form.component.html',
  styleUrl: './purchase-order-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrderFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly supplierService = inject(SupplierService);
  private readonly productService = inject(ProductService);
  private readonly notification = inject(NotificationService);

  readonly suppliers = signal<SupplierDto[]>([]);
  readonly products = signal<ProductDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly purchaseOrderId = signal<string | null>(null);
  readonly currentStatus = signal(PurchaseOrderStatus.Draft);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    supplierId: ['', Validators.required],
    purchaseDate: [this.todayIso(), Validators.required],
    referenceNumber: ['', Validators.maxLength(100)],
    notes: ['', Validators.maxLength(2000)],
    items: this.fb.array([this.createLineItem()]),
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.purchaseOrderId.set(id);

    this.supplierService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.suppliers.set(result.items),
    });
    this.productService.getAll({ pageSize: 500 }).subscribe({
      next: (result) => this.products.set(result.items),
    });

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.purchaseOrderService.getById(id).subscribe({
        next: (po) => {
          this.currentStatus.set(po.status);
          this.items.clear();
          po.items.forEach((item) => {
            this.items.push(
              this.fb.nonNullable.group({
                productId: [item.productId, Validators.required],
                quantity: [item.quantity, [Validators.required, Validators.min(0.01)]],
                unitPrice: [item.unitPrice, [Validators.required, Validators.min(0.01)]],
              }),
            );
          });
          if (this.items.length === 0) this.addLineItem();

          this.form.patchValue({
            supplierId: po.supplierId,
            purchaseDate: po.purchaseDate.slice(0, 10),
            referenceNumber: po.referenceNumber ?? '',
            notes: po.notes ?? '',
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load purchase order.');
          this.loading.set(false);
        },
      });
    }
  }

  get items(): FormArray {
    return this.form.controls.items;
  }

  createLineItem() {
    return this.fb.nonNullable.group({
      productId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [0, [Validators.required, Validators.min(0.01)]],
    });
  }

  addLineItem(): void {
    this.items.push(this.createLineItem());
  }

  removeLineItem(index: number): void {
    if (this.items.length <= 1) return;
    this.items.removeAt(index);
  }

  onProductChange(index: number): void {
    const productId = this.items.at(index).get('productId')?.value;
    const product = this.products().find((p) => p.id === productId);
    if (product) {
      this.items.at(index).patchValue({ unitPrice: product.costPrice });
    }
  }

  lineTotal(index: number): number {
    const item = this.items.at(index).getRawValue();
    return item.quantity * item.unitPrice;
  }

  grandTotal(): number {
    return this.items.controls.reduce((sum, ctrl) => {
      const v = ctrl.getRawValue();
      return sum + v.quantity * v.unitPrice;
    }, 0);
  }

  fieldError(field: string): string | null {
    return getFieldError(this.form.get(field), field);
  }

  lineFieldError(index: number, field: string): string | null {
    return getFieldError(this.items.at(index).get(field), field);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const payload = {
      supplierId: value.supplierId,
      purchaseDate: value.purchaseDate,
      status: this.isEdit() ? this.currentStatus() : PurchaseOrderStatus.Draft,
      referenceNumber: value.referenceNumber || null,
      notes: value.notes || null,
      items: value.items.map((item) => ({
        productId: item.productId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
      })),
    };

    if (this.isEdit() && this.purchaseOrderId()) {
      this.purchaseOrderService.update(this.purchaseOrderId()!, payload).subscribe({
        next: () => {
          this.notification.success('Purchase order updated.');
          this.router.navigate([ROUTES.purchaseOrders.base, this.purchaseOrderId()]);
        },
        error: () => {
          this.notification.error('Failed to update purchase order.');
          this.saving.set(false);
        },
      });
    } else {
      this.purchaseOrderService.create(payload).subscribe({
        next: (res) => {
          this.notification.success('Purchase order created.');
          this.router.navigate([ROUTES.purchaseOrders.base, res.id]);
        },
        error: () => {
          this.notification.error('Failed to create purchase order.');
          this.saving.set(false);
        },
      });
    }
  }

  private todayIso(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
