import { Injectable } from '@angular/core';
import { forkJoin, map, Observable, switchMap } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { PagedResult } from '../models/pagination.model';
import {
  BulkUpdateProductRequest,
  CreateProductRequest,
  CreateProductResponse,
  ProductDto,
  ProductQueryParams,
  UpdateProductRequest,
} from '../models/product.model';

@Injectable({ providedIn: 'root' })
export class ProductService extends BaseApiService {
  getAll(params?: ProductQueryParams): Observable<PagedResult<ProductDto>> {
    return this.get<PagedResult<ProductDto>>(API_ENDPOINTS.products, params);
  }

  getAllForSelect(): Observable<ProductDto[]> {
    return this.getAll({ page: 1, pageSize: 100, sortBy: 'name', sortDirection: 'asc' }).pipe(
      map((result) => result.items ?? []),
    );
  }

  getByCategory(categoryId: string, params?: ProductQueryParams): Observable<PagedResult<ProductDto>> {
    return this.get<PagedResult<ProductDto>>(`${API_ENDPOINTS.products}/by-category/${categoryId}`, params);
  }

  getById(id: string): Observable<ProductDto> {
    return this.get<ProductDto>(`${API_ENDPOINTS.products}/${id}`);
  }

  create(request: CreateProductRequest): Observable<CreateProductResponse> {
    return this.post<CreateProductResponse>(API_ENDPOINTS.products, request);
  }

  update(id: string, request: UpdateProductRequest): Observable<void> {
    return this.put<void>(`${API_ENDPOINTS.products}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return super.delete<void>(`${API_ENDPOINTS.products}/${id}`);
  }

  bulkDelete(ids: string[]): Observable<void[]> {
    return forkJoin(ids.map((id) => this.remove(id)));
  }

  bulkUpdate(ids: string[], changes: BulkUpdateProductRequest): Observable<void[]> {
    return forkJoin(
      ids.map((id) =>
        this.getById(id).pipe(
          switchMap((product) => {
            const request: UpdateProductRequest = {
              categoryId: changes.categoryId ?? product.categoryId,
              name: product.name,
              sku: product.sku,
              description: product.description,
              costPrice: product.costPrice,
              salePrice: product.salePrice,
              reorderLevel: changes.reorderLevel ?? product.reorderLevel,
              isActive: changes.isActive ?? product.isActive,
            };
            return this.update(id, request);
          }),
        ),
      ),
    );
  }
}
