import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError, timer } from 'rxjs';
import { retry, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { RETRY_CONFIG } from '../constants/api.constants';
import { PaginationParams } from '../models/pagination.model';
import { PaginationHelper } from '../helpers/pagination.helper';
import { ApiError } from '../models/api-error.model';

@Injectable({ providedIn: 'root' })
export class BaseApiService {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = environment.apiUrl;

  protected get<T>(endpoint: string, params?: PaginationParams | Record<string, unknown>): Observable<T> {
    const normalizedParams = params ? PaginationHelper.normalizeQueryParams(params) : undefined;
    return this.request<T>('GET', endpoint, undefined, normalizedParams);
  }

  protected post<T>(endpoint: string, body: unknown): Observable<T> {
    return this.request<T>('POST', endpoint, body);
  }

  protected put<T>(endpoint: string, body: unknown): Observable<T> {
    return this.request<T>('PUT', endpoint, body);
  }

  protected patch<T>(endpoint: string, body: unknown): Observable<T> {
    return this.request<T>('PATCH', endpoint, body);
  }

  protected delete<T>(endpoint: string): Observable<T> {
    return this.request<T>('DELETE', endpoint);
  }

  private request<T>(
    method: string,
    endpoint: string,
    body?: unknown,
    params?: PaginationParams | Record<string, unknown>,
  ): Observable<T> {
    const url = `${this.baseUrl}${endpoint}`;
    const httpParams = params ? PaginationHelper.toHttpParams(params) : undefined;

    let request$: Observable<T>;

    switch (method) {
      case 'GET':
        request$ = this.http.get<T>(url, { params: httpParams });
        break;
      case 'POST':
        request$ = this.http.post<T>(url, body);
        break;
      case 'PUT':
        request$ = this.http.put<T>(url, body);
        break;
      case 'PATCH':
        request$ = this.http.patch<T>(url, body);
        break;
      case 'DELETE':
        request$ = this.http.delete<T>(url);
        break;
      default:
        return throwError(() => new Error(`Unsupported HTTP method: ${method}`));
    }

    return request$.pipe(
      retry({
        count: RETRY_CONFIG.maxRetries,
        delay: (error: HttpErrorResponse, retryCount) => {
          if (!RETRY_CONFIG.retryableStatuses.includes(error.status as (typeof RETRY_CONFIG.retryableStatuses)[number])) {
            throw error;
          }
          return timer(RETRY_CONFIG.delayMs * retryCount);
        },
      }),
      catchError((error: HttpErrorResponse) => throwError(() => this.parseError(error))),
    );
  }

  protected parseError(error: HttpErrorResponse): ApiError {
    if (error.status === 0) {
      return {
        status: 0,
        title: 'Cannot reach server',
        detail:
          'The API is unavailable. Start the backend with "dotnet run" in BusinessOS.API (http://localhost:5162), then try again.',
      };
    }

    if (error.status === 500 && (!error.error || typeof error.error === 'string')) {
      return {
        status: 500,
        title: 'Server unavailable',
        detail:
          'The API may not be running. Start the backend with "dotnet run" in BusinessOS.API (http://localhost:5162), then try again.',
      };
    }

    if (error.error && typeof error.error === 'object') {
      const body = error.error as ApiError;
      if (body.status || body.title || body.detail) {
        return body;
      }
    }

    return {
      status: error.status,
      title: error.statusText || 'An unexpected error occurred',
      detail: error.message,
    };
  }
}
