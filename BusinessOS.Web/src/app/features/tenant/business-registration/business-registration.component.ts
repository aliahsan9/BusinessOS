import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TenantService } from '../../../core/services/tenant.service';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { getFieldError, passwordMatchValidator, strongPasswordValidator } from '../../../shared/validators/form.validators';
import { ApiError } from '../../../core/models/api-error.model';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-business-registration',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AppInputComponent,
    AppButtonComponent,
    AppAlertComponent,
    AppCardComponent,
  ],
  templateUrl: './business-registration.component.html',
  styleUrl: './business-registration.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BusinessRegistrationComponent {
  readonly ROUTES = ROUTES;
  private readonly fb = inject(FormBuilder);
  private readonly tenantService = inject(TenantService);
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  readonly errorMessage = signal<string | null>(null);
  readonly loading = signal(false);
  getFieldError = getFieldError;

  readonly form = this.fb.nonNullable.group(
    {
      businessName: ['', [Validators.required, Validators.maxLength(200)]],
      ownerFirstName: ['', [Validators.required, Validators.maxLength(100)]],
      ownerLastName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', [Validators.required, strongPasswordValidator()]],
      confirmPassword: ['', [Validators.required]],
      timezone: ['UTC', Validators.required],
      currency: ['USD', Validators.required],
      industry: ['General', Validators.required],
    },
    { validators: passwordMatchValidator() },
  );

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.loading.set(true);
    const v = this.form.getRawValue();

    this.tenantService
      .registerBusiness({
        businessName: v.businessName,
        ownerFirstName: v.ownerFirstName,
        ownerLastName: v.ownerLastName,
        email: v.email,
        password: v.password,
        timezone: v.timezone,
        currency: v.currency,
        industry: v.industry,
      })
      .subscribe({
        next: (response) => {
          this.authService.establishSession(response, { welcomeMessage: 'Business registered successfully!' });
          void this.router.navigate(['/onboarding']);
          this.loading.set(false);
        },
        error: (error: ApiError) => {
          const message = error.detail ?? error.title ?? 'Registration failed';
          this.errorMessage.set(message);
          this.notificationService.error('Registration failed', message);
          this.loading.set(false);
        },
      });
  }
}
