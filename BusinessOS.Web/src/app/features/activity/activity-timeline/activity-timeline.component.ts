import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivityService } from '../../../core/services/activity.service';
import { ActivityDatePreset, ActivityDto, ActivityEntityType } from '../../../core/models/activity.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { ActivityFiltersComponent } from '../activity-filters/activity-filters.component';

@Component({
  selector: 'app-activity-timeline',
  standalone: true,
  imports: [
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppPaginationComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
    AppButtonComponent,
    ActivityFiltersComponent,
  ],
  templateUrl: './activity-timeline.component.html',
  styleUrl: './activity-timeline.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivityTimelineComponent implements OnInit {
  private readonly activityService = inject(ActivityService);

  readonly ButtonVariant = ButtonVariant;
  readonly items = signal<ActivityDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<string | null>(null);
  readonly search = signal('');
  readonly entityType = signal<ActivityEntityType | ''>('');
  readonly datePreset = signal<ActivityDatePreset>('last7');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');

  readonly breadcrumbs = [{ label: 'Activity', route: ROUTES.activity.list }];

  ngOnInit(): void {
    this.load();
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);

    const range = this.resolveDateRange();

    this.activityService
      .getAll({
        page,
        pageSize: this.pageSize(),
        search: this.search().trim() || undefined,
        entityType: this.entityType() || undefined,
        dateFrom: range.from,
        dateTo: range.to,
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load activity timeline.');
          this.loading.set(false);
        },
      });
  }

  loadMore(): void {
    const nextPage = this.page() + 1;
    if (nextPage > this.totalPages()) {
      return;
    }

    this.loadingMore.set(true);
    const range = this.resolveDateRange();

    this.activityService
      .getAll({
        page: nextPage,
        pageSize: this.pageSize(),
        search: this.search().trim() || undefined,
        entityType: this.entityType() || undefined,
        dateFrom: range.from,
        dateTo: range.to,
      })
      .subscribe({
        next: (result) => {
          this.items.update((items) => [...items, ...result.items]);
          this.page.set(result.page);
          this.totalPages.set(result.totalPages);
          this.loadingMore.set(false);
        },
        error: () => this.loadingMore.set(false),
      });
  }

  onPageChange(page: number): void {
    this.load(page);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  retry(): void {
    this.load(this.page());
  }

  getEntityVariant(entityType: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Customer: 'info',
      Project: 'primary',
      Task: 'warning',
      Invoice: 'success',
      Expense: 'danger',
      Settings: 'neutral',
    };
    return map[entityType] ?? 'neutral';
  }

  private resolveDateRange(): { from?: string; to?: string } {
    const now = new Date();
    const startOfDay = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate());

    switch (this.datePreset()) {
      case 'today': {
        const from = startOfDay(now).toISOString();
        return { from };
      }
      case 'last7': {
        const fromDate = new Date(now);
        fromDate.setDate(fromDate.getDate() - 7);
        return { from: fromDate.toISOString() };
      }
      case 'last30': {
        const fromDate = new Date(now);
        fromDate.setDate(fromDate.getDate() - 30);
        return { from: fromDate.toISOString() };
      }
      case 'custom':
        return {
          from: this.dateFrom() ? new Date(this.dateFrom()).toISOString() : undefined,
          to: this.dateTo() ? new Date(this.dateTo()).toISOString() : undefined,
        };
      default:
        return {};
    }
  }
}
