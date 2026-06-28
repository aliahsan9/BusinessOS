import { PaginationParams } from './pagination.model';
import { PaymentMethod } from '../enums';

export interface PaymentSummaryDto {
  id: string;
  orderId: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  amount: number;
  paymentMethod: PaymentMethod;
  paymentDate: string;
  referenceNo?: string | null;
  createdAt: string;
}

export interface PaymentDto {
  id: string;
  orderId: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  amount: number;
  paymentMethod: PaymentMethod;
  paymentDate: string;
  referenceNo?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreatePaymentRequest {
  orderId: string;
  customerId: string;
  amount: number;
  paymentMethod: PaymentMethod;
  paymentDate: string;
  referenceNo?: string | null;
}

export interface UpdatePaymentRequest extends CreatePaymentRequest {}

export interface PaymentQueryParams extends PaginationParams {
  customerId?: string;
  orderId?: string;
  paymentMethod?: PaymentMethod;
  dateFrom?: string;
  dateTo?: string;
}

export interface CreatePaymentResponse {
  id: string;
}
