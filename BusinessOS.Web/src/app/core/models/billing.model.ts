export interface SubscriptionPlanDto {
  id: string;
  name: string;
  slug: string;
  description?: string;
  monthlyPrice: number;
  annualPrice: number;
  annualSavingsPercent: number;
  maxUsers: number;
  maxCustomers: number;
  maxProjects: number;
  maxTasks: number;
  maxStorageMb: number;
  maxAiRequests: number;
  hasAiAssistant: boolean;
  hasAdvancedAnalytics: boolean;
  hasPdfReports: boolean;
  hasAdvancedReports: boolean;
  hasPrioritySupport: boolean;
  features: string[];
  sortOrder: number;
}

export interface PlanFeatureFlagsDto {
  hasAiAssistant: boolean;
  hasAdvancedAnalytics: boolean;
  hasPdfReports: boolean;
  hasAdvancedReports: boolean;
  hasPrioritySupport: boolean;
}

export interface CurrentPlanDto {
  planId: string;
  planName: string;
  planSlug: string;
  status: string;
  billingInterval: string;
  paymentProvider: string;
  startDate: string;
  trialEndDate?: string;
  currentPeriodEnd?: string;
  renewalDate?: string;
  cancelAtPeriodEnd: boolean;
  isTrialActive: boolean;
  trialDaysRemaining: number;
  currentPrice: number;
  currency: string;
  featureFlags: PlanFeatureFlagsDto;
}

export interface BillingUsageDto {
  userCount: number;
  maxUsers: number;
  customerCount: number;
  maxCustomers: number;
  projectCount: number;
  maxProjects: number;
  taskCount: number;
  maxTasks: number;
  storageUsedMb: number;
  maxStorageMb: number;
  aiRequestsUsed: number;
  maxAiRequests: number;
  lastCalculatedAt: string;
}

export interface BillingInvoiceDto {
  id: string;
  invoiceNumber: string;
  planName: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  currency: string;
  status: string;
  billingInterval: string;
  paymentMethod?: string;
  periodStart: string;
  periodEnd: string;
  paidAt?: string;
  createdAt: string;
}

export interface BillingTransactionDto {
  id: string;
  transactionId: string;
  amount: number;
  currency: string;
  status: string;
  provider: string;
  description?: string;
  createdAt: string;
  completedAt?: string;
}

export interface BillingDashboardDto {
  currentPlan: CurrentPlanDto;
  usage: BillingUsageDto;
  recentInvoices: BillingInvoiceDto[];
  recentTransactions: BillingTransactionDto[];
}

export interface CheckoutSessionDto {
  sessionId: string;
  checkoutUrl?: string;
  provider: string;
}

export interface DowngradeValidationDto {
  isValid: boolean;
  violations: string[];
}

export interface PaymentProviderDto {
  provider: string;
  name: string;
  isEnabled: boolean;
  isSandbox: boolean;
}

export interface UpgradePlanRequest {
  planId: string;
  billingInterval?: string;
  provider?: string;
}

export interface CheckoutRequest {
  planId: string;
  billingInterval?: string;
  provider?: string;
  successUrl?: string;
  cancelUrl?: string;
}

export function formatLimit(value: number): string {
  return value === -1 ? 'Unlimited' : value.toLocaleString();
}

export function usagePercent(current: number, max: number): number {
  if (max === -1 || max === 0) return 0;
  return Math.min(100, Math.round((current / max) * 100));
}
