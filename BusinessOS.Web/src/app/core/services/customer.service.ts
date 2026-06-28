import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreateCustomerRequest,
  CreateCustomerResponse,
  CustomerAnalytics,
  CustomerDto,
  CustomerOrderSummary,
  CustomerQueryParams,
  CustomerSummaryDto,
  UpdateCustomerRequest,
} from '../models/customer.model';

@Injectable({ providedIn: 'root' })
export class CustomerService extends BaseApiService {
  getAll(params?: CustomerQueryParams): Observable<PagedResult<CustomerSummaryDto>> {
    return this.get<PagedResult<CustomerSummaryDto>>(API_ENDPOINTS.customers, params);
  }

  getAllForSelect(): Observable<CustomerSummaryDto[]> {
    return this.getAll({ page: 1, pageSize: 100, sortBy: 'firstName', sortDirection: 'asc' }).pipe(
      map((result) => result.items ?? []),
    );
  }

  getById(id: string): Observable<CustomerDto> {
    return this.get<CustomerDto>(`${API_ENDPOINTS.customers}/${id}`);
  }

  create(request: CreateCustomerRequest): Observable<CreateCustomerResponse> {
    return this.post<CreateCustomerResponse>(API_ENDPOINTS.customers, request);
  }

  update(id: string, request: UpdateCustomerRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.customers}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.customers}/${id}`);
  }

  getOrders(id: string, params?: CustomerQueryParams): Observable<PagedResult<CustomerOrderSummary>> {
    return this.get<PagedResult<CustomerOrderSummary>>(`${API_ENDPOINTS.customers}/${id}/orders`, params);
  }

  getAnalytics(id: string): Observable<CustomerAnalytics> {
    return this.get<CustomerAnalytics>(`${API_ENDPOINTS.customers}/${id}/analytics`);
  }
}
