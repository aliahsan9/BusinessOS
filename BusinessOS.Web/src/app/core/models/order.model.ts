import { PaginationParams } from './pagination.model';
import { OrderStatus } from '../enums';

export interface OrderLineItem {
  id?: string;
  productId: string;
  productName?: string;
  productSku?: string;
  quantity: number;
  unitPrice: number;
  total: number;
}

export interface OrderSummaryDto {
  id: string;
  orderNumber: string;
  orderDate: string;
  createdAt: string;
  status: OrderStatus;
  customerName: string;
  customerEmail: string;
  grandTotal: number;
}

export interface OrderDto {
  id: string;
  customerId: string;
  orderNumber: string;
  orderDate: string;
  createdAt: string;
  updatedAt?: string | null;
  status: OrderStatus;
  customerName: string;
  customerFirstName: string;
  customerLastName: string;
  customerEmail: string;
  customerPhone: string;
  customerAddress: string;
  customerCity: string;
  customerCountry: string;
  totalAmount: number;
  discount: number;
  tax: number;
  grandTotal: number;
  items: OrderLineItem[];
}

export interface CreateOrderItemDto {
  productId: string;
  quantity: number;
}

export interface CreateOrderRequest {
  customerId: string;
  discount: number;
  tax: number;
  items: CreateOrderItemDto[];
}

export interface UpdateOrderRequest {
  discount: number;
  tax: number;
  items: CreateOrderItemDto[];
}

export interface OrderQueryParams extends PaginationParams {
  status?: OrderStatus;
}

export interface CreateOrderResponse {
  id: string;
}
