import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { OnboardingService } from '../../../core/services/onboarding.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { getFieldError } from '../../../shared/validators/form.validators';
import { ApiError } from '../../../core/models/api-error.model';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AppInputComponent, AppButtonComponent, AppAlertComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly onboardingService = inject(OnboardingService);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  readonly errorMessage = signal<string | null>(null);
  readonly rememberMe = signal(!!this.authService.getRememberMeEmail());

  readonly form = this.fb.nonNullable.group({
    email: [this.authService.getRememberMeEmail() ?? '', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    rememberMe: [this.rememberMe()],
  });

  readonly loading = this.authService.loading;
  readonly devCredentials = environment.devCredentials;
  getFieldError = getFieldError;

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    const { email, password, rememberMe } = this.form.getRawValue();

    this.authService.login({ email, password }, rememberMe).subscribe({
      next: () => {
        this.onboardingService.getStatus().subscribe({
          next: (status) => {
            void this.router.navigate([status.shouldShowWizard ? '/onboarding' : '/dashboard']);
          },
          error: () => void this.router.navigate(['/dashboard']),
        });
      },
      error: (error: ApiError) => {
        const message =
          error.status === 401
            ? 'Invalid email or password.'
            : (error.detail ?? error.title ?? 'Sign in failed.');
        this.errorMessage.set(message);
        this.notificationService.error('Sign in failed', message);
      },
    });
  }
}
