import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { UserService } from '../../../core/services/user.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { UserSummaryDto } from '../../../core/models/user.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
  ],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly userService = inject(UserService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<UserSummaryDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly activeFilter = signal('');

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.user.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.user.update);
  readonly breadcrumbs = [{ label: 'Users', route: ROUTES.users.list }, { label: 'Directory' }];
  readonly headerActions = this.canCreate ? [{ label: 'Add User', route: ROUTES.users.create, icon: '➕' }] : [];

  ngOnInit(): void {
    this.load();
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.userService
      .getAll({ page, pageSize: this.pageSize(), search: this.searchTerm() || undefined })
      .subscribe({
        next: (result) => {
          let items = result.items;
          const active = this.activeFilter();
          if (active === 'active') items = items.filter((u) => u.isActive);
          if (active === 'inactive') items = items.filter((u) => !u.isActive);
          this.items.set(items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load users.');
          this.loading.set(false);
        },
      });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.load(1);
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
