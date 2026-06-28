import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreatePurchaseOrderRequest,
  CreatePurchaseOrderResponse,
  PurchaseOrderDto,
  PurchaseOrderQueryParams,
  UpdatePurchaseOrderRequest,
} from '../models/purchase-order.model';
import { PurchaseOrderStatus } from '../enums';

@Injectable({ providedIn: 'root' })
export class PurchaseOrderService extends BaseApiService {
  getAll(params?: PurchaseOrderQueryParams): Observable<PagedResult<PurchaseOrderDto>> {
    return this.get<PagedResult<PurchaseOrderDto>>(API_ENDPOINTS.purchaseOrders, params);
  }

  getById(id: string): Observable<PurchaseOrderDto> {
    return this.get<PurchaseOrderDto>(`${API_ENDPOINTS.purchaseOrders}/${id}`);
  }

  create(request: CreatePurchaseOrderRequest): Observable<CreatePurchaseOrderResponse> {
    return this.post<CreatePurchaseOrderResponse>(API_ENDPOINTS.purchaseOrders, request);
  }

  update(id: string, request: UpdatePurchaseOrderRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.purchaseOrders}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.delete<void>(`${API_ENDPOINTS.purchaseOrders}/${id}`);
  }

  updateStatus(id: string, status: PurchaseOrderStatus): Observable<void> {
    return this.patch<void>(`${API_ENDPOINTS.purchaseOrders}/${id}/status`, { status });
  }

  receive(id: string): Observable<void> {
    return this.post<void>(`${API_ENDPOINTS.purchaseOrders}/${id}/receive`, {});
  }
}
