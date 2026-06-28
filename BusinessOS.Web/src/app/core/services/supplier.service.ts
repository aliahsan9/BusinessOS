import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreateSupplierRequest,
  CreateSupplierResponse,
  SupplierDto,
  SupplierProductSummary,
  SupplierPurchaseSummary,
  SupplierQueryParams,
  UpdateSupplierRequest,
} from '../models/supplier.model';

@Injectable({ providedIn: 'root' })
export class SupplierService extends BaseApiService {
  getAll(params?: SupplierQueryParams): Observable<PagedResult<SupplierDto>> {
    return this.get<PagedResult<SupplierDto>>(API_ENDPOINTS.suppliers, params);
  }

  getById(id: string): Observable<SupplierDto> {
    return this.get<SupplierDto>(`${API_ENDPOINTS.suppliers}/${id}`);
  }

  create(request: CreateSupplierRequest): Observable<CreateSupplierResponse> {
    return this.post<CreateSupplierResponse>(API_ENDPOINTS.suppliers, request);
  }

  update(id: string, request: UpdateSupplierRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.suppliers}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.delete<void>(`${API_ENDPOINTS.suppliers}/${id}`);
  }

  getPurchaseHistory(id: string, params?: SupplierQueryParams): Observable<PagedResult<SupplierPurchaseSummary>> {
    return this.get<PagedResult<SupplierPurchaseSummary>>(`${API_ENDPOINTS.suppliers}/${id}/purchases`, params);
  }

  getProducts(id: string): Observable<SupplierProductSummary[]> {
    return this.get<SupplierProductSummary[]>(`${API_ENDPOINTS.suppliers}/${id}/products`);
  }
}
