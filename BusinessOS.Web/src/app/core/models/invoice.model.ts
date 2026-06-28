import { PaginationParams } from './pagination.model';
import { InvoiceStatus } from '../enums';

export interface InvoiceSummaryDto {
  id: string;
  invoiceNumber: string;
  orderId: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  invoiceDate: string;
  dueDate: string;
  status: InvoiceStatus;
  grandTotal: number;
  amountPaid: number;
  outstandingAmount: number;
  createdAt: string;
}

export interface InvoiceDto {
  id: string;
  invoiceNumber: string;
  orderId: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  invoiceDate: string;
  dueDate: string;
  status: InvoiceStatus;
  subTotal: number;
  discount: number;
  tax: number;
  grandTotal: number;
  amountPaid: number;
  outstandingAmount: number;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateInvoiceFromOrderRequest {
  dueDate: string;
  notes?: string | null;
}

export interface UpdateInvoiceRequest {
  dueDate: string;
  notes?: string | null;
}

export interface InvoiceQueryParams extends PaginationParams {
  status?: InvoiceStatus;
  customerId?: string;
}

export interface CreateInvoiceResponse {
  id: string;
}
