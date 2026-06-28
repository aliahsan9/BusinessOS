import { Injectable, inject, signal, computed } from '@angular/core';
import { CategoryService } from '../core/services/category.service';
import { CategoryDto, CategoryQueryParams } from '../core/models/category.model';
import { PagedResult } from '../core/models/pagination.model';
import { PaginationHelper } from '../core/helpers/pagination.helper';

export interface CategoryListState {
  items: CategoryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  search: string;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
  loading: boolean;
  error: string | null;
}

const initialState: CategoryListState = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  search: '',
  sortBy: 'name',
  sortDirection: 'asc',
  loading: false,
  error: null,
};

@Injectable({ providedIn: 'root' })
export class CategoryStateService {
  private readonly categoryService = inject(CategoryService);
  private readonly state = signal<CategoryListState>(initialState);

  readonly items = computed(() => this.state().items);
  readonly page = computed(() => this.state().page);
  readonly pageSize = computed(() => this.state().pageSize);
  readonly totalCount = computed(() => this.state().totalCount);
  readonly totalPages = computed(() => this.state().totalPages);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);
  readonly search = computed(() => this.state().search);

  load(params?: Partial<CategoryQueryParams>): void {
    const current = this.state();
    const page = PaginationHelper.normalizePage(params?.page ?? current.page);
    const pageSize = PaginationHelper.normalizePageSize(params?.pageSize ?? current.pageSize);
    const search = params?.search ?? current.search;
    const sortBy = params?.sortBy ?? current.sortBy;
    const sortDirection = params?.sortDirection ?? current.sortDirection;

    this.patchState({ loading: true, error: null, page, pageSize, search, sortBy, sortDirection });

    this.categoryService
      .getAll({ page, pageSize, search: search || undefined, sortBy, sortDirection })
      .subscribe({
        next: (result: PagedResult<CategoryDto>) => {
          this.patchState({
            items: result.items,
            totalCount: result.totalCount,
            totalPages: result.totalPages,
            loading: false,
          });
        },
        error: () => this.patchState({ loading: false, error: 'Failed to load categories.' }),
      });
  }

  private patchState(partial: Partial<CategoryListState>): void {
    this.state.update((s) => ({ ...s, ...partial }));
  }
}
