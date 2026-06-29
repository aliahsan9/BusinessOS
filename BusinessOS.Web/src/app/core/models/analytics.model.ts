import { ChartDataResponse, DateRange } from './dashboard.model';

export interface AnalyticsMetric {
  value: number;
  previousValue: number;
  growthPercentage: number;
}

export interface AnalyticsOverviewResponse {
  totalCustomers: AnalyticsMetric;
  activeProjects: AnalyticsMetric;
  totalTasks: AnalyticsMetric;
  completedTasks: AnalyticsMetric;
  totalRevenue: AnalyticsMetric;
  totalExpenses: AnalyticsMetric;
  netProfit: AnalyticsMetric;
  totalInvoices: AnalyticsMetric;
  dateRange: DateRange;
}

export interface AnalyticsTopCustomer {
  customerId: string;
  customerName: string;
  revenueGenerated: number;
  projectsCount: number;
  invoicesCount: number;
}

export interface AnalyticsTopCustomersResponse {
  customers: AnalyticsTopCustomer[];
  dateRange: DateRange;
}

export interface AnalyticsRecentActivity {
  id: string;
  activityType: string;
  title: string;
  description: string;
  occurredAt: string;
}

export interface AnalyticsRecentActivityResponse {
  activities: AnalyticsRecentActivity[];
  dateRange: DateRange;
}

export interface AnalyticsTaskBreakdown {
  total: number;
  completed: number;
  pending: number;
  overdue: number;
}

export interface AnalyticsTaskAnalyticsResponse {
  breakdown: AnalyticsTaskBreakdown;
  chart: ChartDataResponse;
  dateRange: DateRange;
}

export interface AnalyticsProjectStatusBreakdown {
  active: number;
  completed: number;
  onHold: number;
  cancelled: number;
}

export interface AnalyticsProjectAnalyticsResponse {
  statusBreakdown: AnalyticsProjectStatusBreakdown;
  chart: ChartDataResponse;
  dateRange: DateRange;
}

export interface AnalyticsQueryParams {
  startDate?: string;
  endDate?: string;
  period?: string;
  top?: number;
  limit?: number;
  [key: string]: unknown;
}

export interface AnalyticsKpiCard {
  key: string;
  label: string;
  icon: string;
  metric: AnalyticsMetric;
  format: 'currency' | 'number';
}
