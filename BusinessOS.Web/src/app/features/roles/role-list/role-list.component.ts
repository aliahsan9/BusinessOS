import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { RoleService } from '../../../core/services/role.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { RoleDto } from '../../../core/models/role.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-role-list',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleListComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly roleService = inject(RoleService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly items = signal<RoleDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly deleteTarget = signal<RoleDto | null>(null);
  readonly deleting = signal(false);

  readonly routes = ROUTES;
  readonly canCreate = this.tokenService.hasPermission(PermissionCodes.role.create);
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.role.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.role.delete);
  readonly breadcrumbs = [{ label: 'Roles', route: ROUTES.roles.list }, { label: 'Directory' }];
  readonly headerActions = this.canCreate ? [{ label: 'Add Role', route: ROUTES.roles.create, icon: '➕' }] : [];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleService.getAll().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load roles.');
        this.loading.set(false);
      },
    });
  }

  confirmDelete(role: RoleDto): void {
    this.deleteTarget.set(role);
  }

  deleteRole(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.deleting.set(true);
    this.roleService.remove(target.id).subscribe({
      next: () => {
        this.notification.success(`Deleted ${target.name}.`);
        this.deleteTarget.set(null);
        this.deleting.set(false);
        this.load();
      },
      error: () => {
        this.notification.error('Failed to delete role.');
        this.deleting.set(false);
      },
    });
  }

  retry(): void {
    this.load();
  }
}
