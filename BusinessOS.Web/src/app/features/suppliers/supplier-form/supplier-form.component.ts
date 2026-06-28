import { ButtonVariant } from '../../../core/enums';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-supplier-form',
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
  templateUrl: './supplier-form.component.html',
  styleUrl: './supplier-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupplierFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly supplierService = inject(SupplierService);
  private readonly notification = inject(NotificationService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly supplierId = signal<string | null>(null);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
    phone: ['', [Validators.required, Validators.maxLength(50)]],
    address: ['', [Validators.required, Validators.maxLength(500)]],
    contactPerson: ['', Validators.maxLength(200)],
    notes: ['', Validators.maxLength(2000)],
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.supplierId.set(id);

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.supplierService.getById(id).subscribe({
        next: (supplier) => {
          this.form.patchValue({
            name: supplier.name,
            email: supplier.email,
            phone: supplier.phone,
            address: supplier.address,
            contactPerson: supplier.contactPerson ?? '',
            notes: supplier.notes ?? '',
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load supplier.');
          this.loading.set(false);
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
    const value = this.form.getRawValue();
    const payload = {
      ...value,
      contactPerson: value.contactPerson || null,
      notes: value.notes || null,
    };

    if (this.isEdit() && this.supplierId()) {
      this.supplierService.update(this.supplierId()!, payload).subscribe({
        next: () => {
          this.notification.success('Supplier updated.');
          this.router.navigate([ROUTES.suppliers.base, this.supplierId()]);
        },
        error: () => {
          this.notification.error('Failed to update supplier.');
          this.saving.set(false);
        },
      });
    } else {
      this.supplierService.create(payload).subscribe({
        next: (res) => {
          this.notification.success('Supplier created.');
          this.router.navigate([ROUTES.suppliers.base, res.id]);
        },
        error: () => {
          this.notification.error('Failed to create supplier.');
          this.saving.set(false);
        },
      });
    }
  }
}
