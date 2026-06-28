import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import {
  ChartDataResponse,
  CustomerAnalyticsDashboardResponse,
  DashboardOverviewResponse,
  DashboardQueryParams,
  InventoryAnalyticsDashboardResponse,
  OrderAnalyticsResponse,
  ProductAnalyticsResponse,
  SalesAnalyticsResponse,
} from '../models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService extends BaseApiService {
  getOverview(params?: DashboardQueryParams): Observable<DashboardOverviewResponse> {
    return this.get<DashboardOverviewResponse>(API_ENDPOINTS.dashboard.overview, params);
  }

  getSales(params?: DashboardQueryParams): Observable<SalesAnalyticsResponse> {
    return this.get<SalesAnalyticsResponse>(API_ENDPOINTS.dashboard.sales, params);
  }

  getCustomers(params?: DashboardQueryParams): Observable<CustomerAnalyticsDashboardResponse> {
    return this.get<CustomerAnalyticsDashboardResponse>(API_ENDPOINTS.dashboard.customers, params);
  }

  getProducts(params?: DashboardQueryParams): Observable<ProductAnalyticsResponse> {
    return this.get<ProductAnalyticsResponse>(API_ENDPOINTS.dashboard.products, params);
  }

  getInventory(params?: DashboardQueryParams): Observable<InventoryAnalyticsDashboardResponse> {
    return this.get<InventoryAnalyticsDashboardResponse>(API_ENDPOINTS.dashboard.inventory, params);
  }

  getOrders(params?: DashboardQueryParams): Observable<OrderAnalyticsResponse> {
    return this.get<OrderAnalyticsResponse>(API_ENDPOINTS.dashboard.orders, params);
  }

  getRevenueChart(params?: DashboardQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.dashboard.charts.revenue, params);
  }

  getOrdersChart(params?: DashboardQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.dashboard.charts.orders, params);
  }

  getCustomersChart(params?: DashboardQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.dashboard.charts.customers, params);
  }

  getProductsChart(params?: DashboardQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.dashboard.charts.products, params);
  }

  getInventoryChart(params?: DashboardQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.dashboard.charts.inventory, params);
  }
}
