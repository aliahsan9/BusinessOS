import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CreateInvoiceFromOrderRequest,
  CreateInvoiceResponse,
  InvoiceDto,
  InvoiceQueryParams,
  InvoiceSummaryDto,
  UpdateInvoiceRequest,
} from '../models/invoice.model';
import { InvoiceStatus } from '../enums';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class InvoiceService extends BaseApiService {
  getAll(params?: InvoiceQueryParams): Observable<PagedResult<InvoiceSummaryDto>> {
    return this.get<PagedResult<InvoiceSummaryDto>>(API_ENDPOINTS.invoices, params);
  }

  getById(id: string): Observable<InvoiceDto> {
    return this.get<InvoiceDto>(`${API_ENDPOINTS.invoices}/${id}`);
  }

  createFromOrder(orderId: string, request: CreateInvoiceFromOrderRequest): Observable<CreateInvoiceResponse> {
    return this.post<CreateInvoiceResponse>(`${API_ENDPOINTS.invoices}/from-order/${orderId}`, request);
  }

  update(id: string, request: UpdateInvoiceRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.invoices}/${id}`, request);
  }

  updateStatus(id: string, status: InvoiceStatus): Observable<void> {
    return this.patch<void>(`${API_ENDPOINTS.invoices}/${id}/status`, { status });
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.invoices}/${id}`);
  }

  getPdfHtml(id: string): Observable<string> {
    return this.http.get(`${environment.apiUrl}${API_ENDPOINTS.invoices}/${id}/pdf`, {
      responseType: 'text',
    });
  }

  getPdfUrl(id: string): string {
    return `${environment.apiUrl}${API_ENDPOINTS.invoices}/${id}/pdf`;
  }
}
