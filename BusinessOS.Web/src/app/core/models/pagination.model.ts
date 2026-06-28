export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface PaginationParams {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  sortDirection?: 'asc' | 'desc';
}

export interface TableSort {
  column: string;
  direction: 'asc' | 'desc';
}

export interface TableQuery extends PaginationParams {
  filters?: Record<string, string | boolean | number | undefined>;
}
