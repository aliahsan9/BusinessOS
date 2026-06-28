import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { AuditService } from '../../../core/services/audit.service';
import { AuditLogDto } from '../../../core/models/audit.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-audit-log-list',
  standalone: true,
  imports: [
    FormsModule,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
  ],
  templateUrl: './audit-log-list.component.html',
  styleUrl: './audit-log-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly auditService = inject(AuditService);

  readonly items = signal<AuditLogDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionFilter = signal('');
  readonly entityTypeFilter = signal('');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly routes = ROUTES;
  readonly breadcrumbs = [{ label: 'Audit Logs', route: ROUTES.audit.list }];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.auditService
      .getAll({
        page,
        pageSize: this.pageSize(),
        action: this.actionFilter() || undefined,
        entityType: this.entityTypeFilter() || undefined,
        dateFrom: this.dateFrom() || undefined,
        dateTo: this.dateTo() || undefined,
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
          this.error.set('Failed to load audit logs.');
          this.loading.set(false);
        },
      });
  }

  onFilterChange(): void {
    this.load(1);
  }

  onPageChange(page: number): void {
    this.load(page);
  }

  retry(): void {
    this.load();
  }
}
