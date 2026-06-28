export interface TrendPoint {
  period: string;
  amount: number;
  count: number;
}

export interface FinancialDashboard {
  totalRevenue: number;
  totalExpenses: number;
  netProfit: number;
  totalPayments: number;
  outstandingInvoices: number;
  completedOrders: number;
  totalExpensesCount: number;
  period: string;
  startDate: string;
  endDate: string;
  revenueTrend: TrendPoint[];
  expenseTrend: TrendPoint[];
}

export interface ProfitLossReport {
  totalRevenue: number;
  totalExpenses: number;
  grossProfit: number;
  netProfit: number;
  groupBy: string;
  period: string;
  startDate: string;
  endDate: string;
  periodBreakdown: TrendPoint[];
}

export interface RevenueCategoryItem {
  category: string;
  amount: number;
  count: number;
}

export interface RevenueBreakdown {
  orderRevenue: number;
  paymentTotal: number;
  byPaymentMethod: RevenueCategoryItem[];
  trends: TrendPoint[];
  period: string;
  startDate: string;
  endDate: string;
}

export interface ExpenseCategoryItem {
  categoryName: string;
  amount: number;
  count: number;
  percentage: number;
}

export interface ExpenseBreakdown {
  totalExpenses: number;
  totalCount: number;
  byCategory: ExpenseCategoryItem[];
  trends: TrendPoint[];
  period: string;
  startDate: string;
  endDate: string;
}

export interface FinanceQueryParams {
  startDate?: string;
  endDate?: string;
  period?: string;
  groupBy?: string;
}
