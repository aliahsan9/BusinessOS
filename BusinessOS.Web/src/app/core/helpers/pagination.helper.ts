import { HttpParams } from '@angular/common/http';
import { PaginationParams } from '../models/pagination.model';
import { environment } from '../../../environments/environment';

export class PaginationHelper {
  static toHttpParams(params: PaginationParams | Record<string, unknown> = {}): HttpParams {
    let httpParams = new HttpParams();

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    });

    return httpParams;
  }

  static normalizePage(page?: number): number {
    return page && page > 0 ? page : 1;
  }

  static normalizePageSize(pageSize?: number): number {
    if (!pageSize || pageSize <= 0) {
      return environment.defaultPageSize;
    }
    return Math.min(pageSize, environment.maxPageSize);
  }

  static normalizeQueryParams(
    params: PaginationParams | Record<string, unknown>,
  ): Record<string, unknown> {
    const normalized = { ...params };
    const hasPage = 'page' in normalized;
    const hasPageSize = 'pageSize' in normalized;

    if (hasPage || hasPageSize) {
      normalized['page'] = PaginationHelper.normalizePage(
        hasPage ? (normalized['page'] as number | undefined) : undefined,
      );
      normalized['pageSize'] = PaginationHelper.normalizePageSize(
        hasPageSize ? (normalized['pageSize'] as number | undefined) : undefined,
      );
    }

    return normalized;
  }
}
