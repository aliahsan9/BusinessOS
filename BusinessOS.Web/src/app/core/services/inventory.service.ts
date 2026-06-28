import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  InventoryAnalytics,
  InventoryDetail,
  InventoryQueryParams,
  InventorySummary,
  StockAdjustmentRequest,
  StockChangeRequest,
  StockTransaction,
  StockTransactionQueryParams,
  UpdateInventoryRequest,
} from '../models/inventory.model';

@Injectable({ providedIn: 'root' })
export class InventoryService extends BaseApiService {
  getAll(params?: InventoryQueryParams): Observable<PagedResult<InventorySummary>> {
    return this.get<PagedResult<InventorySummary>>(API_ENDPOINTS.inventory.base, params);
  }

  getByProductId(productId: string): Observable<InventoryDetail> {
    return this.get<InventoryDetail>(`${API_ENDPOINTS.inventory.base}/${productId}`);
  }

  update(productId: string, request: UpdateInventoryRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.inventory.base}/${productId}`, request);
  }

  increaseStock(request: StockChangeRequest): Observable<void> {
    return this.post<void>(API_ENDPOINTS.inventory.increase, request);
  }

  decreaseStock(request: StockChangeRequest): Observable<void> {
    return this.post<void>(API_ENDPOINTS.inventory.decrease, request);
  }

  adjustStock(request: StockAdjustmentRequest): Observable<void> {
    return this.post<void>(API_ENDPOINTS.inventory.adjust, request);
  }

  getTransactions(params?: StockTransactionQueryParams): Observable<PagedResult<StockTransaction>> {
    return this.get<PagedResult<StockTransaction>>(API_ENDPOINTS.inventory.transactions, params);
  }

  getAnalytics(): Observable<InventoryAnalytics> {
    return this.get<InventoryAnalytics>(API_ENDPOINTS.inventory.analytics);
  }

  getLowStock(): Observable<InventorySummary[]> {
    return this.get<InventorySummary[]>(API_ENDPOINTS.inventory.lowStock);
  }

  getOutOfStock(): Observable<InventorySummary[]> {
    return this.get<InventorySummary[]>(API_ENDPOINTS.inventory.outOfStock);
  }

  getReorderProducts(): Observable<InventorySummary[]> {
    return this.get<InventorySummary[]>(API_ENDPOINTS.inventory.reorderProducts);
  }
}
