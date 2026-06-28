import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreateExpenseRequest,
  CreateExpenseResponse,
  ExpenseAnalytics,
  ExpenseDto,
  ExpenseQueryParams,
  ExpenseSummaryDto,
  UpdateExpenseRequest,
} from '../models/expense.model';

@Injectable({ providedIn: 'root' })
export class ExpenseService extends BaseApiService {
  getAll(params?: ExpenseQueryParams): Observable<PagedResult<ExpenseSummaryDto>> {
    return this.get<PagedResult<ExpenseSummaryDto>>(API_ENDPOINTS.expenses.base, params);
  }

  getById(id: string): Observable<ExpenseDto> {
    return this.get<ExpenseDto>(`${API_ENDPOINTS.expenses.base}/${id}`);
  }

  create(request: CreateExpenseRequest): Observable<CreateExpenseResponse> {
    return this.post<CreateExpenseResponse>(API_ENDPOINTS.expenses.base, request);
  }

  update(id: string, request: UpdateExpenseRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.expenses.base}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.expenses.base}/${id}`);
  }

  getAnalytics(params?: { dateFrom?: string; dateTo?: string }): Observable<ExpenseAnalytics> {
    return this.get<ExpenseAnalytics>(API_ENDPOINTS.expenses.analytics, params);
  }
}
