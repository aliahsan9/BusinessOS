export interface DateRange {
  startDate: string;
  endDate: string;
  period: string;
}

export interface DashboardOverviewResponse {
  totalProducts: number;
  totalCategories: number;
  totalCustomers: number;
  totalOrders: number;
  totalRevenue: number;
  totalInventoryValue: number;
  totalActiveUsers: number;
  lowStockProducts: number;
  outOfStockProducts: number;
  dateRange: DateRange;
}

export interface SalesAnalyticsResponse {
  todaySales: number;
  weeklySales: number;
  monthlySales: number;
  yearlySales: number;
  averageOrderValue: number;
  completedOrders: number;
  cancelledOrders: number;
  revenueTrends: RevenueTrend[];
  dateRange: DateRange;
}

export interface RevenueTrend {
  date: string;
  revenue: number;
  orderCount: number;
}

export interface CustomerAnalyticsDashboardResponse {
  totalCustomers: number;
  newCustomers: number;
  activeCustomers: number;
  customerGrowthRate: number;
  averageLifetimeValue: number;
  averageSpending: number;
  topCustomers: TopCustomer[];
  dateRange: DateRange;
}

export interface TopCustomer {
  customerId: string;
  fullName: string;
  email: string;
  totalOrders: number;
  totalSpending: number;
}

export interface ProductPerformanceDto {
  productId: string;
  productName: string;
  sku: string;
  totalQuantitySold: number;
  totalRevenue: number;
  currentStock: number;
}

export interface ProductAnalyticsResponse {
  bestSellingProducts: ProductPerformanceDto[];
  worstSellingProducts: ProductPerformanceDto[];
  mostOrderedProducts: ProductPerformanceDto[];
  highestRevenueProducts: ProductPerformanceDto[];
  productRankings: ProductPerformanceDto[];
  topLimit: number;
  dateRange: DateRange;
}

export interface InventoryAnalyticsDashboardResponse {
  totalInventoryValue: number;
  totalStockQuantity: number;
  lowStockCount: number;
  outOfStockCount: number;
  stockLevels: StockLevel[];
  reorderRecommendations: ReorderRecommendation[];
  stockMovementTrends: StockMovementTrend[];
  dateRange: DateRange;
}

export interface StockLevel {
  productId: string;
  productName: string;
  currentStock: number;
  reorderLevel: number;
  isLowStock: boolean;
  isOutOfStock: boolean;
}

export interface ReorderRecommendation {
  productId: string;
  productName: string;
  currentStock: number;
  suggestedReorderQuantity: number;
}

export interface StockMovementTrend {
  date: string;
  inbound: number;
  outbound: number;
}

export interface OrderStatusCount {
  status: string;
  count: number;
}

export interface OrderAnalyticsResponse {
  ordersByStatus: OrderStatusCount[];
  ordersPerDay: { date: string; count: number }[];
  ordersPerMonth: { month: string; count: number }[];
  successRate: number;
  cancellationRate: number;
  dateRange: DateRange;
}

export interface ChartDataset {
  label: string;
  data: number[];
  chartStyle: string;
}

export interface ChartDataResponse {
  chartType: string;
  title: string;
  labels: string[];
  datasets: ChartDataset[];
  dateRange: DateRange;
}

export interface DashboardQueryParams {
  startDate?: string;
  endDate?: string;
  period?: string;
  top?: 10 | 20;
  [key: string]: unknown;
}

export interface DashboardNotification {
  id: string;
  title: string;
  message: string;
  type: 'info' | 'success' | 'warning' | 'danger';
  createdAt: string;
  read: boolean;
}

export interface RecentOrderSummary {
  id: string;
  orderNumber: string;
  customerName: string;
  status: string;
  grandTotal: number;
  orderDate: string;
}
