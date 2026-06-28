import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
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

  getAllForSelect(): Observable<CategoryDto[]> {
    return this.getAll({ page: 1, pageSize: 100 }).pipe(map((result) => result.items));
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

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.categories}/${id}`);
  }
}
