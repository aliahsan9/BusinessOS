import { PaginationParams } from './pagination.model';

export interface ProductDto {
  id: string;
  categoryId: string;
  name: string;
  sku: string;
  description?: string | null;
  costPrice: number;
  salePrice: number;
  currentStock: number;
  reorderLevel: number;
  isActive: boolean;
}

export interface CreateProductRequest {
  categoryId: string;
  name: string;
  sku: string;
  description?: string | null;
  costPrice: number;
  salePrice: number;
  reorderLevel: number;
}

export interface UpdateProductRequest {
  categoryId: string;
  name: string;
  sku: string;
  description?: string | null;
  costPrice: number;
  salePrice: number;
  reorderLevel: number;
  isActive: boolean;
}

export interface ProductQueryParams extends PaginationParams {
  categoryId?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreateProductResponse {
  id: string;
}

export interface BulkUpdateProductRequest {
  categoryId?: string;
  isActive?: boolean;
  reorderLevel?: number;
}
