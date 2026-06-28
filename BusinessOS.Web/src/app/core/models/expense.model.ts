import { ExpenseStatus, PaymentMethod } from '../enums';
import { PaginationParams } from './pagination.model';

export interface ExpenseSummaryDto {
  id: string;
  title: string;
  amount: number;
  expenseDate: string;
  categoryName: string;
  paymentMethod: string;
  vendor?: string | null;
  status: ExpenseStatus | string;
  isRecurring: boolean;
  createdAt: string;
}

export interface ExpenseDto {
  id: string;
  title: string;
  amount: number;
  expenseDate: string;
  expenseCategoryId: string;
  categoryName: string;
  paymentMethod: string;
  vendor?: string | null;
  referenceNumber?: string | null;
  description?: string | null;
  receiptUrl?: string | null;
  status: ExpenseStatus | string;
  isRecurring: boolean;
  recurrencePattern?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ExpenseCategoryDto {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  expenseCount: number;
  totalAmount: number;
  createdAt: string;
}

export interface ExpenseQueryParams extends PaginationParams {
  categoryId?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  sortOrder?: 'asc' | 'desc';
}

export interface CreateExpenseRequest {
  title: string;
  amount: number;
  expenseDate: string;
  expenseCategoryId: string;
  paymentMethod: PaymentMethod | string;
  vendor?: string | null;
  referenceNumber?: string | null;
  description?: string | null;
  receiptUrl?: string | null;
  status: ExpenseStatus | string;
  isRecurring: boolean;
  recurrencePattern?: string | null;
}

export type UpdateExpenseRequest = CreateExpenseRequest;

export interface CreateExpenseResponse {
  id: string;
}

export interface CreateExpenseCategoryRequest {
  name: string;
  description?: string | null;
}

export interface UpdateExpenseCategoryRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateExpenseCategoryResponse {
  id: string;
}

export interface ExpenseCategoryBreakdown {
  categoryName: string;
  amount: number;
  count: number;
  percentage: number;
}

export interface ExpenseTrendPoint {
  period: string;
  amount: number;
  count: number;
}

export interface ExpenseAnalytics {
  totalExpenses: number;
  monthlyExpenses: number;
  totalCount: number;
  topCategories: ExpenseCategoryBreakdown[];
  trends: ExpenseTrendPoint[];
}
