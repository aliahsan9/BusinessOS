import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  ConvertQuotationResponse,
  CreateQuotationRequest,
  CreateQuotationResponse,
  QuotationDto,
  QuotationQueryParams,
  QuotationSummaryDto,
  UpdateQuotationRequest,
} from '../models/quotation.model';
import { QuotationStatus } from '../enums';

@Injectable({ providedIn: 'root' })
export class QuotationService extends BaseApiService {
  getAll(params?: QuotationQueryParams): Observable<PagedResult<QuotationSummaryDto>> {
    return this.get<PagedResult<QuotationSummaryDto>>(API_ENDPOINTS.quotations, params);
  }

  getById(id: string): Observable<QuotationDto> {
    return this.get<QuotationDto>(`${API_ENDPOINTS.quotations}/${id}`);
  }

  create(request: CreateQuotationRequest): Observable<CreateQuotationResponse> {
    return this.post<CreateQuotationResponse>(API_ENDPOINTS.quotations, request);
  }

  update(id: string, request: UpdateQuotationRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.quotations}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.quotations}/${id}`);
  }

  updateStatus(id: string, status: QuotationStatus): Observable<void> {
    return this.patch<void>(`${API_ENDPOINTS.quotations}/${id}/status`, { status });
  }

  convertToOrder(id: string): Observable<ConvertQuotationResponse> {
    return this.post<ConvertQuotationResponse>(`${API_ENDPOINTS.quotations}/${id}/convert-to-order`, {});
  }
}
