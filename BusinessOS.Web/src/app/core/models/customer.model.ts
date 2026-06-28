import { PaginationParams } from './pagination.model';

export interface CustomerSummaryDto {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  city: string;
  country: string;
  isActive: boolean;
  createdAt: string;
}

export interface CustomerDto {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  address: string;
  city: string;
  country: string;
  postalCode: string;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface CreateCustomerRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  address: string;
  city: string;
  country: string;
  postalCode: string;
}

export interface UpdateCustomerRequest extends CreateCustomerRequest {
  isActive: boolean;
}

export interface CustomerQueryParams extends PaginationParams {
  city?: string;
  country?: string;
  isActive?: boolean;
}

export interface CreateCustomerResponse {
  id: string;
}

export interface CustomerOrderSummary {
  id: string;
  orderNumber: string;
  orderDate: string;
  status: string;
  grandTotal: number;
  createdAt: string;
}

export interface CustomerAnalytics {
  totalOrders: number;
  totalSpending: number;
  averageOrderValue: number;
  lastOrderDate?: string | null;
  totalCompletedOrders: number;
}
