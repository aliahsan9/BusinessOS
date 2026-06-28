import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import {
  CreateExpenseCategoryRequest,
  CreateExpenseCategoryResponse,
  ExpenseCategoryDto,
  UpdateExpenseCategoryRequest,
} from '../models/expense.model';

@Injectable({ providedIn: 'root' })
export class ExpenseCategoryService extends BaseApiService {
  getAll(activeOnly?: boolean): Observable<ExpenseCategoryDto[]> {
    return this.get<ExpenseCategoryDto[]>(API_ENDPOINTS.expenseCategories, { activeOnly });
  }

  getById(id: string): Observable<ExpenseCategoryDto> {
    return this.get<ExpenseCategoryDto>(`${API_ENDPOINTS.expenseCategories}/${id}`);
  }

  create(request: CreateExpenseCategoryRequest): Observable<CreateExpenseCategoryResponse> {
    return this.post<CreateExpenseCategoryResponse>(API_ENDPOINTS.expenseCategories, request);
  }

  update(id: string, request: UpdateExpenseCategoryRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.expenseCategories}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.expenseCategories}/${id}`);
  }
}
