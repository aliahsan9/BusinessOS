export interface OnboardingStatusDto {
  currentStep: number;
  isCompleted: boolean;
  isSkipped: boolean;
  completedAt?: string | null;
  shouldShowWizard: boolean;
}

export interface SaveOnboardingProgressRequest {
  currentStep: number;
  isSkipped?: boolean;
}

export interface OnboardingBusinessProfileDto {
  name: string;
  logoUrl?: string | null;
  website?: string | null;
  industry: string;
  description?: string | null;
  currency: string;
  timezone: string;
}

export interface SaveOnboardingBusinessProfileRequest {
  name: string;
  logoUrl?: string | null;
  website?: string | null;
  industry: string;
  description?: string | null;
  currency: string;
  timezone: string;
}

export const ONBOARDING_TOTAL_STEPS = 8;

export const ONBOARDING_STEP_LABELS = [
  'Welcome',
  'Business Profile',
  'First Customer',
  'First Project',
  'First Task',
  'First Invoice',
  'Analytics',
  'Complete',
] as const;
