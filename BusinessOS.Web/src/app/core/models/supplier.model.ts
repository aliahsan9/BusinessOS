import { PaginationParams } from './pagination.model';

export interface SupplierDto {
  id: string;
  name: string;
  email: string;
  phone: string;
  address: string;
  contactPerson?: string | null;
  notes?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateSupplierRequest {
  name: string;
  email: string;
  phone: string;
  address: string;
  contactPerson?: string | null;
  notes?: string | null;
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {}

export interface SupplierQueryParams extends PaginationParams {
  sortDirection?: 'asc' | 'desc';
}

export interface CreateSupplierResponse {
  id: string;
}

export interface SupplierPurchaseSummary {
  id: string;
  purchaseDate: string;
  totalAmount: number;
  status: string;
  itemCount: number;
}

export interface SupplierProductSummary {
  productId: string;
  productName: string;
  productSku: string;
  lastPurchaseDate?: string | null;
  totalQuantityPurchased: number;
}
