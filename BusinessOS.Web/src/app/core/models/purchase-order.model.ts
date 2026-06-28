import { PaginationParams } from './pagination.model';
import { PurchaseOrderStatus } from '../enums';

export interface PurchaseOrderLineItem {
  id?: string;
  productId: string;
  productName?: string;
  productSku?: string;
  quantity: number;
  unitPrice: number;
  total: number;
}

export interface PurchaseOrderDto {
  id: string;
  supplierId: string;
  supplierName?: string;
  purchaseDate: string;
  totalAmount: number;
  status: PurchaseOrderStatus;
  referenceNumber?: string | null;
  notes?: string | null;
  items: PurchaseOrderLineItem[];
  createdAt?: string;
  updatedAt?: string;
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  purchaseDate: string;
  status: PurchaseOrderStatus;
  referenceNumber?: string | null;
  notes?: string | null;
  items: Omit<PurchaseOrderLineItem, 'id' | 'productName' | 'productSku' | 'total'>[];
}

export interface UpdatePurchaseOrderRequest extends CreatePurchaseOrderRequest {}

export interface PurchaseOrderQueryParams extends PaginationParams {
  supplierId?: string;
  status?: PurchaseOrderStatus;
  sortDirection?: 'asc' | 'desc';
}

export interface CreatePurchaseOrderResponse {
  id: string;
}
