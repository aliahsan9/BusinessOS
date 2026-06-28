import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { OrderService } from '../../../core/services/order.service';
import { CustomerService } from '../../../core/services/customer.service';
import { ProductService } from '../../../core/services/product.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CustomerSummaryDto } from '../../../core/models/customer.model';
import { ProductDto } from '../../../core/models/product.model';
import { ButtonVariant } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-order-form',
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
    AppAlertComponent,
  ],
  templateUrl: './order-form.component.html',
  styleUrl: './order-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly orderService = inject(OrderService);
  private readonly customerService = inject(CustomerService);
  private readonly productService = inject(ProductService);
  private readonly notification = inject(NotificationService);

  readonly customers = signal<CustomerSummaryDto[]>([]);
  readonly products = signal<ProductDto[]>([]);
  readonly loading = signal(false);
  readonly lookupLoading = signal(true);
  readonly lookupError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly orderId = signal<string | null>(null);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    discount: [0, [Validators.required, Validators.min(0)]],
    tax: [0, [Validators.required, Validators.min(0)]],
    items: this.fb.array([this.createLineItem()]),
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.orderId.set(id);

    const customerIdFromQuery = this.route.snapshot.queryParamMap.get('customerId');
    if (customerIdFromQuery && !this.isEdit()) {
      this.form.patchValue({ customerId: customerIdFromQuery });
    }

    this.loadLookups();

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.orderService.getById(id).subscribe({
        next: (order) => {
          this.items.clear();
          order.items.forEach((item) => {
            this.items.push(
              this.fb.nonNullable.group({
                productId: [item.productId, Validators.required],
                quantity: [item.quantity, [Validators.required, Validators.min(0.01)]],
              }),
            );
          });
          if (this.items.length === 0) this.addLineItem();

          this.form.patchValue({
            customerId: order.customerId,
            discount: order.discount,
            tax: order.tax,
          });
          this.form.controls.customerId.disable();
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load order.');
          this.loading.set(false);
        },
      });
    }
  }

  loadLookups(): void {
    this.lookupLoading.set(true);
    this.lookupError.set(null);

    forkJoin({
      customers: this.customerService.getAllForSelect(),
      products: this.productService.getAllForSelect(),
    }).subscribe({
      next: ({ customers, products }) => {
        this.customers.set(customers);
        this.products.set(products);
        this.lookupLoading.set(false);
      },
      error: () => {
        this.lookupError.set('Failed to load customers and products.');
        this.lookupLoading.set(false);
        this.notification.error('Failed to load customers and products for this form.');
      },
    });
  }

  get items(): FormArray {
    return this.form.controls.items;
  }

  createLineItem() {
    return this.fb.nonNullable.group({
      productId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
    });
  }

  addLineItem(): void {
    this.items.push(this.createLineItem());
  }

  removeLineItem(index: number): void {
    if (this.items.length <= 1) return;
    this.items.removeAt(index);
  }

  productSalePrice(productId: string): number {
    return this.products().find((p) => p.id === productId)?.salePrice ?? 0;
  }

  lineTotal(index: number): number {
    const item = this.items.at(index).getRawValue();
    return item.quantity * this.productSalePrice(item.productId);
  }

  subtotal(): number {
    return this.items.controls.reduce((sum, ctrl) => {
      const v = ctrl.getRawValue();
      return sum + v.quantity * this.productSalePrice(v.productId);
    }, 0);
  }

  grandTotal(): number {
    const value = this.form.getRawValue();
    return this.subtotal() - value.discount + value.tax;
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
    const items = value.items.map((item) => ({
      productId: item.productId,
      quantity: item.quantity,
    }));

    if (this.isEdit() && this.orderId()) {
      this.orderService
        .update(this.orderId()!, {
          discount: value.discount,
          tax: value.tax,
          items,
        })
        .subscribe({
          next: () => {
            this.notification.success('Order updated.');
            this.router.navigate([ROUTES.orders.base, this.orderId()]);
          },
          error: () => {
            this.notification.error('Failed to update order.');
            this.saving.set(false);
          },
        });
    } else {
      this.orderService
        .create({
          customerId: value.customerId,
          discount: value.discount,
          tax: value.tax,
          items,
        })
        .subscribe({
          next: (res) => {
            this.notification.success('Order created.');
            this.router.navigate([ROUTES.orders.base, res.id]);
          },
          error: () => {
            this.notification.error('Failed to create order.');
            this.saving.set(false);
          },
        });
    }
  }
}
