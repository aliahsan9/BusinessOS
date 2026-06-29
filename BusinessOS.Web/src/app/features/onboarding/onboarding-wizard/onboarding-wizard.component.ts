import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { WelcomeStepComponent } from '../steps/welcome-step/welcome-step.component';
import { BusinessSetupStepComponent } from '../steps/business-setup-step/business-setup-step.component';
import { CustomerStepComponent } from '../steps/customer-step/customer-step.component';
import { ProjectStepComponent } from '../steps/project-step/project-step.component';
import { TaskStepComponent } from '../steps/task-step/task-step.component';
import { InvoiceStepComponent } from '../steps/invoice-step/invoice-step.component';
import { AnalyticsIntroStepComponent } from '../steps/analytics-intro-step/analytics-intro-step.component';
import { CompletionStepComponent } from '../steps/completion-step/completion-step.component';
import { OnboardingService } from '../../../core/services/onboarding.service';
import {
  ONBOARDING_STEP_LABELS,
  ONBOARDING_TOTAL_STEPS,
} from '../../../core/models/onboarding.model';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-onboarding-wizard',
  standalone: true,
  imports: [
    WelcomeStepComponent,
    BusinessSetupStepComponent,
    CustomerStepComponent,
    ProjectStepComponent,
    TaskStepComponent,
    InvoiceStepComponent,
    AnalyticsIntroStepComponent,
    CompletionStepComponent,
  ],
  templateUrl: './onboarding-wizard.component.html',
  styleUrl: './onboarding-wizard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OnboardingWizardComponent {
  private readonly onboardingService = inject(OnboardingService);
  private readonly router = inject(Router);

  readonly totalSteps = ONBOARDING_TOTAL_STEPS;
  readonly stepLabels = ONBOARDING_STEP_LABELS;
  readonly currentStep = signal(1);
  readonly saving = signal(false);

  constructor() {
    this.onboardingService.getStatus().subscribe({
      next: (status) => {
        if (status.isCompleted) {
          void this.router.navigate([ROUTES.dashboard]);
          return;
        }
        this.currentStep.set(Math.max(1, status.currentStep));
      },
    });
  }

  progressPercent(): number {
    return Math.round((this.currentStep() / this.totalSteps) * 100);
  }

  goToStep(step: number): void {
    const nextStep = Math.min(Math.max(step, 1), this.totalSteps);
    this.persistStep(nextStep);
  }

  skipOnboarding(): void {
    this.saving.set(true);
    this.onboardingService.saveProgress({ currentStep: this.currentStep(), isSkipped: true }).subscribe({
      next: () => {
        this.saving.set(false);
        void this.router.navigate([ROUTES.dashboard]);
      },
      error: () => this.saving.set(false),
    });
  }

  resumeLater(): void {
    this.skipOnboarding();
  }

  skipCurrentStep(): void {
    this.goToStep(this.currentStep() + 1);
  }

  completeOnboarding(): void {
    this.saving.set(true);
    this.onboardingService.complete().subscribe({
      next: () => {
        this.saving.set(false);
        void this.router.navigate([ROUTES.dashboard]);
      },
      error: () => this.saving.set(false),
    });
  }

  private persistStep(step: number): void {
    this.currentStep.set(step);
    this.onboardingService.saveProgress({ currentStep: step }).subscribe();
  }
}
