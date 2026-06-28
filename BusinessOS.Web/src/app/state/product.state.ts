import { Injectable, inject, signal, computed } from '@angular/core';
import { ProductService } from '../core/services/product.service';
import { ProductDto, ProductQueryParams } from '../core/models/product.model';
import { PagedResult } from '../core/models/pagination.model';
import { PaginationHelper } from '../core/helpers/pagination.helper';

export interface ProductListState {
  items: ProductDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  search: string;
  categoryId: string | null | undefined;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
  loading: boolean;
  error: string | null;
}

const initialState: ProductListState = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  search: '',
  categoryId: null,
  sortBy: 'name',
  sortDirection: 'asc',
  loading: false,
  error: null,
};

@Injectable({ providedIn: 'root' })
export class ProductStateService {
  private readonly productService = inject(ProductService);
  private readonly state = signal<ProductListState>(initialState);

  readonly items = computed(() => this.state().items);
  readonly page = computed(() => this.state().page);
  readonly pageSize = computed(() => this.state().pageSize);
  readonly totalCount = computed(() => this.state().totalCount);
  readonly totalPages = computed(() => this.state().totalPages);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);
  readonly search = computed(() => this.state().search);
  readonly categoryId = computed(() => this.state().categoryId);

  load(params?: Partial<ProductQueryParams>): void {
    const current = this.state();
    const page = PaginationHelper.normalizePage(params?.page ?? current.page);
    const pageSize = PaginationHelper.normalizePageSize(params?.pageSize ?? current.pageSize);
    const search = params?.search ?? current.search;
    const categoryId = params?.categoryId !== undefined ? params.categoryId : current.categoryId;
    const sortBy = params?.sortBy ?? current.sortBy;
    const sortDirection = params?.sortDirection ?? params?.sortOrder ?? current.sortDirection;

    this.patchState({ loading: true, error: null, page, pageSize, search, categoryId, sortBy, sortDirection });

    this.productService
      .getAll({
        page,
        pageSize,
        search: search || undefined,
        categoryId: categoryId || undefined,
        sortBy,
        sortDirection,
      })
      .subscribe({
        next: (result: PagedResult<ProductDto>) => {
          this.patchState({
            items: result.items,
            totalCount: result.totalCount,
            totalPages: result.totalPages,
            loading: false,
          });
        },
        error: () => this.patchState({ loading: false, error: 'Failed to load products.' }),
      });
  }

  private patchState(partial: Partial<ProductListState>): void {
    this.state.update((s) => ({ ...s, ...partial }));
  }
}
