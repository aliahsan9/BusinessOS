import { ChangeDetectionStrategy, Component, input, OnInit, output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../../shared/components/app-button/app-button.component';
import { AppInputComponent } from '../../../../shared/components/app-input/app-input.component';
import { ButtonVariant } from '../../../../core/enums';
import { OnboardingService } from '../../../../core/services/onboarding.service';
import { OnboardingBusinessProfileDto } from '../../../../core/models/onboarding.model';
import { getFieldError } from '../../../../shared/validators/form.validators';
import { DEFAULT_CURRENCY_CODE } from '../../../../core/constants/currency.constants';

@Component({
  selector: 'app-business-setup-step',
  standalone: true,
  imports: [ReactiveFormsModule, AppButtonComponent, AppInputComponent],
  templateUrl: './business-setup-step.component.html',
  styleUrl: './business-setup-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BusinessSetupStepComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly onboardingService = inject(OnboardingService);

  readonly saving = signal(false);
  readonly loading = signal(true);

  readonly next = output<void>();
  readonly back = output<void>();
  readonly skipStep = output<void>();

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    logoUrl: [''],
    website: [''],
    industry: ['', Validators.required],
    description: [''],
    currency: [DEFAULT_CURRENCY_CODE, Validators.required],
    timezone: ['UTC', Validators.required],
  });

  getFieldError = getFieldError;

  ngOnInit(): void {
    this.onboardingService.getBusinessProfile().subscribe({
      next: (profile) => this.patchForm(profile),
      error: () => this.loading.set(false),
      complete: () => this.loading.set(false),
    });
  }

  saveAndContinue(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    this.onboardingService
      .saveBusinessProfile({
        name: value.name,
        logoUrl: value.logoUrl || null,
        website: value.website || null,
        industry: value.industry,
        description: value.description || null,
        currency: value.currency,
        timezone: value.timezone,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.next.emit();
        },
        error: () => this.saving.set(false),
      });
  }

  private patchForm(profile: OnboardingBusinessProfileDto): void {
    this.form.patchValue({
      name: profile.name,
      logoUrl: profile.logoUrl ?? '',
      website: profile.website ?? '',
      industry: profile.industry,
      description: profile.description ?? '',
      currency: profile.currency,
      timezone: profile.timezone,
    });
  }
}
