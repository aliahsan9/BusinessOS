/** Shared domain models — re-exported from core for feature-layer imports */
export type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  AuthUser,
} from '../core/models/auth.model';

export type { ApiError, ApiErrorResponse } from '../core/models/api-error.model';

export type { PagedResult, PaginationParams } from '../core/models/pagination.model';

export type {
  DashboardOverviewResponse,
  SalesAnalyticsResponse,
  OrderAnalyticsResponse,
  ChartDataResponse,
} from '../core/models/dashboard.model';
