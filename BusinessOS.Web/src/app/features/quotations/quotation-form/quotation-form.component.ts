import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { QuotationService } from '../../../core/services/quotation.service';
import { CustomerService } from '../../../core/services/customer.service';
import { ProductService } from '../../../core/services/product.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CustomerSummaryDto } from '../../../core/models/customer.model';
import { ProductDto } from '../../../core/models/product.model';
import { ButtonVariant, QuotationStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-quotation-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AppCurrencyPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppInputComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
  ],
  templateUrl: './quotation-form.component.html',
  styleUrl: './quotation-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuotationFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly quotationService = inject(QuotationService);
  private readonly customerService = inject(CustomerService);
  private readonly productService = inject(ProductService);
  private readonly notification = inject(NotificationService);

  readonly customers = signal<CustomerSummaryDto[]>([]);
  readonly products = signal<ProductDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly quotationId = signal<string | null>(null);
  readonly currentStatus = signal(QuotationStatus.Draft);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    quotationDate: [this.todayIso(), Validators.required],
    expiryDate: [this.defaultExpiryIso(), Validators.required],
    discount: [0, [Validators.required, Validators.min(0)]],
    tax: [0, [Validators.required, Validators.min(0)]],
    notes: ['', Validators.maxLength(2000)],
    items: this.fb.array([this.createLineItem()]),
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.quotationId.set(id);

    this.customerService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.customers.set(result.items),
    });
    this.productService.getAll({ pageSize: 500 }).subscribe({
      next: (result) => this.products.set(result.items),
    });

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.quotationService.getById(id).subscribe({
        next: (quotation) => {
          this.currentStatus.set(quotation.status);
          this.items.clear();
          quotation.items.forEach((item) => {
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
            customerId: quotation.customerId,
            quotationDate: quotation.quotationDate.slice(0, 10),
            expiryDate: quotation.expiryDate.slice(0, 10),
            discount: quotation.discount,
            tax: quotation.tax,
            notes: quotation.notes ?? '',
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load quotation.');
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
      this.items.at(index).patchValue({ unitPrice: product.salePrice });
    }
  }

  lineTotal(index: number): number {
    const item = this.items.at(index).getRawValue();
    return item.quantity * item.unitPrice;
  }

  subTotal(): number {
    return this.items.controls.reduce((sum, ctrl) => {
      const v = ctrl.getRawValue();
      return sum + v.quantity * v.unitPrice;
    }, 0);
  }

  grandTotal(): number {
    const value = this.form.getRawValue();
    return this.subTotal() - value.discount + value.tax;
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
      customerId: value.customerId,
      quotationDate: value.quotationDate,
      expiryDate: value.expiryDate,
      status: this.isEdit() ? this.currentStatus() : QuotationStatus.Draft,
      discount: value.discount,
      tax: value.tax,
      notes: value.notes || null,
      items: value.items.map((item) => ({
        productId: item.productId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
      })),
    };

    if (this.isEdit() && this.quotationId()) {
      this.quotationService.update(this.quotationId()!, payload).subscribe({
        next: () => {
          this.notification.success('Quotation updated.');
          this.router.navigate([ROUTES.quotations.base, this.quotationId()]);
        },
        error: () => {
          this.notification.error('Failed to update quotation.');
          this.saving.set(false);
        },
      });
    } else {
      this.quotationService.create(payload).subscribe({
        next: (res) => {
          this.notification.success('Quotation created.');
          this.router.navigate([ROUTES.quotations.base, res.id]);
        },
        error: () => {
          this.notification.error('Failed to create quotation.');
          this.saving.set(false);
        },
      });
    }
  }

  private todayIso(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private defaultExpiryIso(): string {
    const date = new Date();
    date.setDate(date.getDate() + 30);
    return date.toISOString().slice(0, 10);
  }
}
