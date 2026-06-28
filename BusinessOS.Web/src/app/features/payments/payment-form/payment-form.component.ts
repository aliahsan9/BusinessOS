import { ButtonVariant, PaymentMethod } from '../../../core/enums';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PaymentService } from '../../../core/services/payment.service';
import { OrderService } from '../../../core/services/order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { OrderSummaryDto } from '../../../core/models/order.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-payment-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppInputComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
  ],
  templateUrl: './payment-form.component.html',
  styleUrl: './payment-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  readonly PaymentMethod = PaymentMethod;
  readonly methodOptions = Object.values(PaymentMethod);

  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly paymentService = inject(PaymentService);
  private readonly orderService = inject(OrderService);
  private readonly notification = inject(NotificationService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly paymentId = signal<string | null>(null);
  readonly orders = signal<OrderSummaryDto[]>([]);
  readonly selectedCustomerName = signal('');
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    orderId: ['', Validators.required],
    customerId: [{ value: '', disabled: true }, Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paymentMethod: [PaymentMethod.Cash, Validators.required],
    paymentDate: [new Date().toISOString().slice(0, 10), Validators.required],
    referenceNo: ['', Validators.maxLength(100)],
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.paymentId.set(id);

    const queryOrderId = this.route.snapshot.queryParamMap.get('orderId');
    const queryCustomerId = this.route.snapshot.queryParamMap.get('customerId');

    this.orderService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.orders.set(result.items);
        if (queryOrderId) {
          this.form.patchValue({ orderId: queryOrderId });
          this.onOrderChange(queryOrderId);
        }
      },
    });

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.paymentService.getById(id).subscribe({
        next: (payment) => {
          this.form.patchValue({
            orderId: payment.orderId,
            customerId: payment.customerId,
            amount: payment.amount,
            paymentMethod: payment.paymentMethod,
            paymentDate: payment.paymentDate.slice(0, 10),
            referenceNo: payment.referenceNo ?? '',
          });
          this.selectedCustomerName.set(payment.customerName);
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load payment.');
          this.loading.set(false);
        },
      });
    } else if (queryOrderId) {
      this.onOrderChange(queryOrderId, queryCustomerId ?? undefined);
    }
  }

  onOrderChange(orderId: string, fallbackCustomerId?: string): void {
    const order = this.orders().find((o) => o.id === orderId);
    if (order) {
      this.form.patchValue({ customerId: fallbackCustomerId ?? '' });
      this.orderService.getById(orderId).subscribe({
        next: (detail) => {
          this.form.patchValue({ customerId: detail.customerId });
          this.selectedCustomerName.set(detail.customerName);
        },
      });
    }
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
    const raw = this.form.getRawValue();
    const payload = {
      orderId: raw.orderId,
      customerId: raw.customerId,
      amount: raw.amount,
      paymentMethod: raw.paymentMethod as PaymentMethod,
      paymentDate: raw.paymentDate,
      referenceNo: raw.referenceNo || null,
    };

    if (this.isEdit() && this.paymentId()) {
      this.paymentService.update(this.paymentId()!, payload).subscribe({
        next: () => {
          this.notification.success('Payment updated.');
          this.router.navigate([ROUTES.payments.base, this.paymentId()]);
        },
        error: () => {
          this.notification.error('Failed to update payment.');
          this.saving.set(false);
        },
      });
    } else {
      this.paymentService.create(payload).subscribe({
        next: (res) => {
          this.notification.success('Payment recorded.');
          this.router.navigate([ROUTES.payments.base, res.id]);
        },
        error: () => {
          this.notification.error('Failed to record payment.');
          this.saving.set(false);
        },
      });
    }
  }
}
