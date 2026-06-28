export interface ApiError {
  type?: string;
  status: number;
  title: string;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface ApiErrorResponse {
  error: ApiError;
}
