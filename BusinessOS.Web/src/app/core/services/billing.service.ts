import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import {
  BillingDashboardDto,
  BillingInvoiceDto,
  BillingTransactionDto,
  BillingUsageDto,
  CheckoutRequest,
  CheckoutSessionDto,
  CurrentPlanDto,
  DowngradeValidationDto,
  PaymentProviderDto,
  SubscriptionPlanDto,
  UpgradePlanRequest,
} from '../models/billing.model';

@Injectable({ providedIn: 'root' })
export class BillingService extends BaseApiService {
  getPlans(): Observable<SubscriptionPlanDto[]> {
    return this.get<SubscriptionPlanDto[]>('/billing/plans');
  }

  getCurrentPlan(): Observable<CurrentPlanDto> {
    return this.get<CurrentPlanDto>('/billing/current-plan');
  }

  getUsage(): Observable<BillingUsageDto> {
    return this.get<BillingUsageDto>('/billing/usage');
  }

  getDashboard(): Observable<BillingDashboardDto> {
    return this.get<BillingDashboardDto>('/billing/dashboard');
  }

  getInvoices(): Observable<BillingInvoiceDto[]> {
    return this.get<BillingInvoiceDto[]>('/billing/invoices');
  }

  getTransactions(): Observable<BillingTransactionDto[]> {
    return this.get<BillingTransactionDto[]>('/billing/transactions');
  }

  getPaymentProviders(): Observable<PaymentProviderDto[]> {
    return this.get<PaymentProviderDto[]>('/billing/providers');
  }

  upgradePlan(request: UpgradePlanRequest): Observable<CurrentPlanDto> {
    return this.post<CurrentPlanDto>('/billing/upgrade', request);
  }

  downgradePlan(planId: string): Observable<CurrentPlanDto> {
    return this.post<CurrentPlanDto>('/billing/downgrade', { planId });
  }

  cancelPlan(cancelImmediately = false): Observable<CurrentPlanDto> {
    return this.post<CurrentPlanDto>('/billing/cancel', { cancelImmediately });
  }

  renewPlan(billingInterval = 'monthly', provider = 'stripe'): Observable<CurrentPlanDto> {
    return this.post<CurrentPlanDto>('/billing/renew', { billingInterval, provider });
  }

  checkout(request: CheckoutRequest): Observable<CheckoutSessionDto> {
    return this.post<CheckoutSessionDto>('/billing/checkout', request);
  }

  createPortal(returnUrl: string): Observable<{ portalUrl: string }> {
    return this.post<{ portalUrl: string }>('/billing/portal', { returnUrl });
  }

  validateDowngrade(planId: string): Observable<DowngradeValidationDto> {
    return this.post<DowngradeValidationDto>(`/billing/validate-downgrade/${planId}`, {});
  }
}
