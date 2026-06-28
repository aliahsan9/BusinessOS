import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  CategoryDto,
  CategoryQueryParams,
  CreateCategoryRequest,
  CreateCategoryResponse,
  UpdateCategoryRequest,
} from '../models/category.model';

@Injectable({ providedIn: 'root' })
export class CategoryService extends BaseApiService {
  getAll(params?: CategoryQueryParams): Observable<PagedResult<CategoryDto>> {
    return this.get<PagedResult<CategoryDto>>(API_ENDPOINTS.categories, params);
  }

  getAllUnpaged(): Observable<CategoryDto[]> {
    return this.get<PagedResult<CategoryDto>>(API_ENDPOINTS.categories, { page: 1, pageSize: 100 }).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (source: any) => new Observable<CategoryDto[]>((subscriber) => {
        source.subscribe({
          next: (result: PagedResult<CategoryDto>) => {
            subscriber.next(result.items);
            subscriber.complete();
          },
          error: (err: unknown) => subscriber.error(err),
        });
      }),
    );
  }

  getById(id: string): Observable<CategoryDto> {
    return this.get<CategoryDto>(`${API_ENDPOINTS.categories}/${id}`);
  }

  create(request: CreateCategoryRequest): Observable<CreateCategoryResponse> {
    return this.post<CreateCategoryResponse>(API_ENDPOINTS.categories, request);
  }

  update(id: string, request: UpdateCategoryRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.categories}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.delete<void>(`${API_ENDPOINTS.categories}/${id}`);
  }
}
