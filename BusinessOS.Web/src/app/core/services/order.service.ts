import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreateOrderRequest,
  CreateOrderResponse,
  OrderDto,
  OrderQueryParams,
  OrderSummaryDto,
  UpdateOrderRequest,
} from '../models/order.model';
import { OrderStatus } from '../enums';

@Injectable({ providedIn: 'root' })
export class OrderService extends BaseApiService {
  getAll(params?: OrderQueryParams): Observable<PagedResult<OrderSummaryDto>> {
    return this.get<PagedResult<OrderSummaryDto>>(API_ENDPOINTS.orders, params);
  }

  getById(id: string): Observable<OrderDto> {
    return this.get<OrderDto>(`${API_ENDPOINTS.orders}/${id}`);
  }

  create(request: CreateOrderRequest): Observable<CreateOrderResponse> {
    return this.post<CreateOrderResponse>(API_ENDPOINTS.orders, request);
  }

  update(id: string, request: UpdateOrderRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.orders}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.orders}/${id}`);
  }

  updateStatus(id: string, status: OrderStatus): Observable<void> {
    return this.patch<void>(`${API_ENDPOINTS.orders}/${id}/status`, { status });
  }
}
