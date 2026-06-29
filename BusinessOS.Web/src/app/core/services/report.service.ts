import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api.constants';
import { ReportHistoryResponse, ReportQueryParams, ReportType } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getHistory(): Observable<ReportHistoryResponse> {
    return this.http.get<ReportHistoryResponse>(`${this.baseUrl}${API_ENDPOINTS.reports.history}`);
  }

  generateReport(type: ReportType, params?: ReportQueryParams): Observable<void> {
    const endpoint = this.resolveEndpoint(type);
    return this.downloadPdf(`${this.baseUrl}${endpoint}`, this.toHttpParams(params));
  }

  generateInvoicePdf(invoiceId: string): Observable<void> {
    return this.downloadPdf(`${this.baseUrl}${API_ENDPOINTS.reports.invoice}/${invoiceId}`);
  }

  downloadHistory(id: string): Observable<void> {
    return this.downloadPdf(`${this.baseUrl}${API_ENDPOINTS.reports.historyDownload}/${id}/download`);
  }

  regenerateHistory(id: string): Observable<void> {
    return this.downloadPdf(
      `${this.baseUrl}${API_ENDPOINTS.reports.historyRegenerate}/${id}/regenerate`,
      undefined,
      'POST',
    );
  }

  private resolveEndpoint(type: ReportType): string {
    const map: Record<ReportType, string> = {
      [ReportType.BusinessSummary]: API_ENDPOINTS.reports.businessSummary,
      [ReportType.Revenue]: API_ENDPOINTS.reports.revenue,
      [ReportType.Expenses]: API_ENDPOINTS.reports.expenses,
      [ReportType.ProfitLoss]: API_ENDPOINTS.reports.profitLoss,
      [ReportType.Customers]: API_ENDPOINTS.reports.customers,
      [ReportType.Projects]: API_ENDPOINTS.reports.projects,
      [ReportType.Tasks]: API_ENDPOINTS.reports.tasks,
      [ReportType.Invoice]: API_ENDPOINTS.reports.invoice,
    };
    return map[type];
  }

  private toHttpParams(params?: ReportQueryParams): HttpParams | undefined {
    if (!params) return undefined;

    let httpParams = new HttpParams();
    if (params.period) httpParams = httpParams.set('period', params.period);
    if (params.startDate) httpParams = httpParams.set('startDate', params.startDate);
    if (params.endDate) httpParams = httpParams.set('endDate', params.endDate);
    if (params.customerId) httpParams = httpParams.set('customerId', params.customerId);
    if (params.projectId) httpParams = httpParams.set('projectId', params.projectId);
    return httpParams;
  }

  private downloadPdf(
    url: string,
    params?: HttpParams,
    method: 'GET' | 'POST' = 'GET',
  ): Observable<void> {
    const request$ =
      method === 'POST'
        ? this.http.post(url, null, { params, observe: 'response', responseType: 'blob' })
        : this.http.get(url, { params, observe: 'response', responseType: 'blob' });

    return request$.pipe(
      tap((response) => this.triggerDownload(response)),
      map(() => void 0),
    );
  }

  private triggerDownload(response: HttpResponse<Blob>): void {
    const blob = response.body;
    if (!blob) return;

    const fileName = this.extractFileName(response) ?? `report-${Date.now()}.pdf`;
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
  }

  private extractFileName(response: HttpResponse<Blob>): string | null {
    const disposition = response.headers.get('Content-Disposition');
    if (!disposition) return null;

    const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/i.exec(disposition);
    if (!match?.[1]) return null;

    return match[1].replace(/['"]/g, '');
  }
}
