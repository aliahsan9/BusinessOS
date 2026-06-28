import { PaginationParams } from './pagination.model';

export interface CategoryDto {
  id: string;
  name: string;
  description?: string | null;
}

export interface CreateCategoryRequest {
  name: string;
  description?: string | null;
}

export interface UpdateCategoryRequest {
  name: string;
  description?: string | null;
}

export interface CategoryQueryParams extends PaginationParams {
  sortDirection?: 'asc' | 'desc';
}

export interface CreateCategoryResponse {
  id: string;
}
