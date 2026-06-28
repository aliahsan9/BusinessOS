import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ExpenseService } from '../../../core/services/expense.service';
import { ExpenseCategoryService } from '../../../core/services/expense-category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ExpenseCategoryDto } from '../../../core/models/expense.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant, ExpenseStatus, PaymentMethod } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-expense-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AppBreadcrumbComponent, AppPageHeaderComponent, AppInputComponent, AppButtonComponent, AppCardComponent, AppSkeletonComponent],
  templateUrl: './expense-form.component.html',
  styleUrl: './expense-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpenseFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  readonly ExpenseStatus = ExpenseStatus;
  readonly PaymentMethod = PaymentMethod;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly expenseService = inject(ExpenseService);
  private readonly categoryService = inject(ExpenseCategoryService);
  private readonly notification = inject(NotificationService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly expenseId = signal<string | null>(null);
  readonly categories = signal<ExpenseCategoryDto[]>([]);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    expenseDate: ['', Validators.required],
    expenseCategoryId: ['', Validators.required],
    paymentMethod: [PaymentMethod.Cash, Validators.required],
    vendor: [''],
    referenceNumber: [''],
    description: [''],
    receiptUrl: [''],
    status: [ExpenseStatus.Pending, Validators.required],
    isRecurring: [false],
    recurrencePattern: [''],
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.expenseId.set(id);

    this.categoryService.getAll(true).subscribe({ next: (c) => this.categories.set(c) });

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.expenseService.getById(id).subscribe({
        next: (expense) => {
          this.form.patchValue({
            title: expense.title,
            amount: expense.amount,
            expenseDate: expense.expenseDate.slice(0, 10),
            expenseCategoryId: expense.expenseCategoryId,
            paymentMethod: expense.paymentMethod as PaymentMethod,
            vendor: expense.vendor ?? '',
            referenceNumber: expense.referenceNumber ?? '',
            description: expense.description ?? '',
            receiptUrl: expense.receiptUrl ?? '',
            status: expense.status as ExpenseStatus,
            isRecurring: expense.isRecurring,
            recurrencePattern: expense.recurrencePattern ?? '',
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load expense.');
          this.loading.set(false);
        },
      });
    } else {
      this.form.patchValue({ expenseDate: new Date().toISOString().slice(0, 10) });
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
    const value = this.form.getRawValue();
    const payload = { ...value, vendor: value.vendor || null, referenceNumber: value.referenceNumber || null, description: value.description || null, receiptUrl: value.receiptUrl || null, recurrencePattern: value.recurrencePattern || null };

    if (this.isEdit() && this.expenseId()) {
      this.expenseService.update(this.expenseId()!, payload).subscribe({
        next: () => {
          this.notification.success('Expense updated.');
          this.router.navigate([ROUTES.expenses.base, this.expenseId()]);
        },
        error: () => { this.notification.error('Failed to update expense.'); this.saving.set(false); },
      });
    } else {
      this.expenseService.create(payload).subscribe({
        next: (res) => {
          this.notification.success('Expense created.');
          this.router.navigate([ROUTES.expenses.base, res.id]);
        },
        error: () => { this.notification.error('Failed to create expense.'); this.saving.set(false); },
      });
    }
  }
}
