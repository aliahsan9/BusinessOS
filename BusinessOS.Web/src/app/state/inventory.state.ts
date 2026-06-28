import { Injectable, inject, signal, computed } from '@angular/core';
import { InventoryService } from '../core/services/inventory.service';
import {
  InventoryAnalytics,
  InventoryQueryParams,
  InventorySummary,
  StockTransaction,
  StockTransactionQueryParams,
} from '../core/models/inventory.model';
import { PagedResult } from '../core/models/pagination.model';
import { PaginationHelper } from '../core/helpers/pagination.helper';

export interface InventoryListState {
  items: InventorySummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  search: string;
  lowStock: boolean | null;
  outOfStock: boolean | null;
  sortBy: string;
  sortOrder: 'asc' | 'desc';
  loading: boolean;
  error: string | null;
}

export interface StockHistoryState {
  items: StockTransaction[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  productId: string | null;
  transactionType: string | null;
  search: string;
  loading: boolean;
  error: string | null;
}

const listInitial: InventoryListState = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  search: '',
  lowStock: null,
  outOfStock: null,
  sortBy: 'productName',
  sortOrder: 'asc',
  loading: false,
  error: null,
};

const historyInitial: StockHistoryState = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  productId: null,
  transactionType: null,
  search: '',
  loading: false,
  error: null,
};

@Injectable({ providedIn: 'root' })
export class InventoryStateService {
  private readonly inventoryService = inject(InventoryService);
  private readonly listState = signal<InventoryListState>(listInitial);
  private readonly historyState = signal<StockHistoryState>(historyInitial);
  private readonly analyticsState = signal<InventoryAnalytics | null>(null);
  private readonly analyticsLoading = signal(false);

  readonly items = computed(() => this.listState().items);
  readonly page = computed(() => this.listState().page);
  readonly pageSize = computed(() => this.listState().pageSize);
  readonly totalCount = computed(() => this.listState().totalCount);
  readonly totalPages = computed(() => this.listState().totalPages);
  readonly loading = computed(() => this.listState().loading);
  readonly error = computed(() => this.listState().error);

  readonly transactions = computed(() => this.historyState().items);
  readonly historyPage = computed(() => this.historyState().page);
  readonly historyTotalPages = computed(() => this.historyState().totalPages);
  readonly historyTotalCount = computed(() => this.historyState().totalCount);
  readonly historyLoading = computed(() => this.historyState().loading);

  readonly analytics = computed(() => this.analyticsState());
  readonly analyticsLoadingState = computed(() => this.analyticsLoading());

  loadInventory(params?: Partial<InventoryQueryParams>): void {
    const current = this.listState();
    const page = PaginationHelper.normalizePage(params?.page ?? current.page);
    const pageSize = PaginationHelper.normalizePageSize(params?.pageSize ?? current.pageSize);

    this.listState.update((s) => ({ ...s, loading: true, error: null, page, pageSize, ...params }));

    const state = this.listState();
    this.inventoryService
      .getAll({
        page: state.page,
        pageSize: state.pageSize,
        search: state.search || undefined,
        lowStock: state.lowStock ?? undefined,
        outOfStock: state.outOfStock ?? undefined,
        sortBy: state.sortBy,
        sortOrder: state.sortOrder,
      })
      .subscribe({
        next: (result: PagedResult<InventorySummary>) => {
          this.listState.update((s) => ({
            ...s,
            items: result.items,
            totalCount: result.totalCount,
            totalPages: result.totalPages,
            loading: false,
          }));
        },
        error: () => this.listState.update((s) => ({ ...s, loading: false, error: 'Failed to load inventory.' })),
      });
  }

  loadTransactions(params?: Partial<StockTransactionQueryParams>): void {
    const current = this.historyState();
    const page = PaginationHelper.normalizePage(params?.page ?? current.page);
    const pageSize = PaginationHelper.normalizePageSize(params?.pageSize ?? current.pageSize);

    this.historyState.update((s) => ({ ...s, loading: true, error: null, page, pageSize, ...params }));

    const state = this.historyState();
    this.inventoryService
      .getTransactions({
        page: state.page,
        pageSize: state.pageSize,
        productId: state.productId || undefined,
        transactionType: state.transactionType || undefined,
        search: state.search || undefined,
      })
      .subscribe({
        next: (result: PagedResult<StockTransaction>) => {
          this.historyState.update((s) => ({
            ...s,
            items: result.items,
            totalCount: result.totalCount,
            totalPages: result.totalPages,
            loading: false,
          }));
        },
        error: () => this.historyState.update((s) => ({ ...s, loading: false, error: 'Failed to load transactions.' })),
      });
  }

  loadAnalytics(): void {
    this.analyticsLoading.set(true);
    this.inventoryService.getAnalytics().subscribe({
      next: (data) => {
        this.analyticsState.set(data);
        this.analyticsLoading.set(false);
      },
      error: () => this.analyticsLoading.set(false),
    });
  }
}
