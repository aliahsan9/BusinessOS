import { PaginationParams } from './pagination.model';
import { QuotationStatus } from '../enums';

export interface QuotationLineItem {
  id?: string;
  productId: string;
  productName?: string;
  productSku?: string;
  quantity: number;
  unitPrice: number;
  total: number;
}

export interface QuotationSummaryDto {
  id: string;
  quotationNumber: string;
  customerId: string;
  customerName: string;
  quotationDate: string;
  expiryDate: string;
  status: QuotationStatus;
  grandTotal: number;
  createdAt: string;
}

export interface QuotationDto {
  id: string;
  quotationNumber: string;
  customerId: string;
  customerName: string;
  quotationDate: string;
  expiryDate: string;
  status: QuotationStatus;
  subTotal: number;
  discount: number;
  tax: number;
  grandTotal: number;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  items: QuotationLineItem[];
}

export interface QuotationLineItemRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateQuotationRequest {
  customerId: string;
  quotationDate: string;
  expiryDate: string;
  status: QuotationStatus;
  discount: number;
  tax: number;
  notes?: string | null;
  items: QuotationLineItemRequest[];
}

export interface UpdateQuotationRequest extends CreateQuotationRequest {}

export interface QuotationQueryParams extends PaginationParams {
  status?: QuotationStatus;
  customerId?: string;
}

export interface CreateQuotationResponse {
  id: string;
}

export interface ConvertQuotationResponse {
  id: string;
}
