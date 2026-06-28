import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import {
  ExpenseBreakdown,
  FinanceQueryParams,
  FinancialDashboard,
  ProfitLossReport,
  RevenueBreakdown,
} from '../models/finance.model';

@Injectable({ providedIn: 'root' })
export class FinanceService extends BaseApiService {
  getDashboard(params?: FinanceQueryParams): Observable<FinancialDashboard> {
    return this.get<FinancialDashboard>(API_ENDPOINTS.finance.dashboard, params as Record<string, unknown>);
  }

  getProfitLoss(params?: FinanceQueryParams): Observable<ProfitLossReport> {
    return this.get<ProfitLossReport>(API_ENDPOINTS.finance.profitLoss, params as Record<string, unknown>);
  }

  getRevenueBreakdown(params?: FinanceQueryParams): Observable<RevenueBreakdown> {
    return this.get<RevenueBreakdown>(API_ENDPOINTS.finance.revenueBreakdown, params as Record<string, unknown>);
  }

  getExpenseBreakdown(params?: FinanceQueryParams): Observable<ExpenseBreakdown> {
    return this.get<ExpenseBreakdown>(API_ENDPOINTS.finance.expenseBreakdown, params as Record<string, unknown>);
  }
}
