import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { ChartDataResponse } from '../models/dashboard.model';
import {
  AnalyticsOverviewResponse,
  AnalyticsProjectAnalyticsResponse,
  AnalyticsQueryParams,
  AnalyticsRecentActivityResponse,
  AnalyticsTaskAnalyticsResponse,
  AnalyticsTopCustomersResponse,
} from '../models/analytics.model';

@Injectable({ providedIn: 'root' })
export class AnalyticsService extends BaseApiService {
  getOverview(params?: AnalyticsQueryParams): Observable<AnalyticsOverviewResponse> {
    return this.get<AnalyticsOverviewResponse>(API_ENDPOINTS.analytics.overview, params);
  }

  getRevenueChart(params?: AnalyticsQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.analytics.revenue, params);
  }

  getExpenseChart(params?: AnalyticsQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.analytics.expenses, params);
  }

  getProfitChart(params?: AnalyticsQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.analytics.profit, params);
  }

  getCustomerGrowthChart(params?: AnalyticsQueryParams): Observable<ChartDataResponse> {
    return this.get<ChartDataResponse>(API_ENDPOINTS.analytics.customers, params);
  }

  getProjectAnalytics(params?: AnalyticsQueryParams): Observable<AnalyticsProjectAnalyticsResponse> {
    return this.get<AnalyticsProjectAnalyticsResponse>(API_ENDPOINTS.analytics.projects, params);
  }

  getTaskAnalytics(params?: AnalyticsQueryParams): Observable<AnalyticsTaskAnalyticsResponse> {
    return this.get<AnalyticsTaskAnalyticsResponse>(API_ENDPOINTS.analytics.tasks, params);
  }

  getTopCustomers(params?: AnalyticsQueryParams): Observable<AnalyticsTopCustomersResponse> {
    return this.get<AnalyticsTopCustomersResponse>(API_ENDPOINTS.analytics.topCustomers, params);
  }

  getRecentActivity(params?: AnalyticsQueryParams): Observable<AnalyticsRecentActivityResponse> {
    return this.get<AnalyticsRecentActivityResponse>(API_ENDPOINTS.analytics.recentActivity, params);
  }
}
