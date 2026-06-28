import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreatePaymentRequest,
  CreatePaymentResponse,
  PaymentDto,
  PaymentQueryParams,
  PaymentSummaryDto,
  UpdatePaymentRequest,
} from '../models/payment.model';

@Injectable({ providedIn: 'root' })
export class PaymentService extends BaseApiService {
  getAll(params?: PaymentQueryParams): Observable<PagedResult<PaymentSummaryDto>> {
    return this.get<PagedResult<PaymentSummaryDto>>(API_ENDPOINTS.payments, params);
  }

  getById(id: string): Observable<PaymentDto> {
    return this.get<PaymentDto>(`${API_ENDPOINTS.payments}/${id}`);
  }

  create(request: CreatePaymentRequest): Observable<CreatePaymentResponse> {
    return this.post<CreatePaymentResponse>(API_ENDPOINTS.payments, request);
  }

  update(id: string, request: UpdatePaymentRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.payments}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.payments}/${id}`);
  }
}
