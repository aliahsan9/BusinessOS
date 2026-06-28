import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { RoleService } from '../../../core/services/role.service';
import { PermissionService } from '../../../core/services/permission.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { RoleDto } from '../../../core/models/role.model';
import { PermissionDto } from '../../../core/models/role.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';

@Component({
  selector: 'app-role-detail',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppButtonComponent,
    AppSkeletonComponent,
  ],
  templateUrl: './role-detail.component.html',
  styleUrl: './role-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly roleService = inject(RoleService);
  private readonly permissionService = inject(PermissionService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly role = signal<RoleDto | null>(null);
  readonly permissions = signal<PermissionDto[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.role.update);

  readonly groupedPermissions = computed(() => {
    const groups = new Map<string, PermissionDto[]>();
    for (const p of this.permissions()) {
      const list = groups.get(p.category) ?? [];
      list.push(p);
      groups.set(p.category, list);
    }
    return Array.from(groups.entries()).sort(([a], [b]) => a.localeCompare(b));
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.permissionService.getAll().subscribe({ next: (p) => this.permissions.set(p) });

    this.roleService.getById(id).subscribe({
      next: (r) => {
        this.role.set(r);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    if (!this.canUpdate) return [];
    return [
      {
        label: 'Edit',
        route: `${ROUTES.roles.base}/${this.role()?.id}/edit`,
        icon: '✏️',
        variant: ButtonVariant.Outline,
      },
    ];
  }

  hasPermission(code: string): boolean {
    return this.role()?.permissions.includes(code) ?? false;
  }

  togglePermission(permission: PermissionDto): void {
    const role = this.role();
    if (!role || !this.canUpdate) return;

    this.saving.set(true);
    const assigned = this.hasPermission(permission.code);

    const request$ = assigned
      ? this.roleService.removePermission(role.id, permission.id)
      : this.roleService.assignPermission(role.id, { permissionId: permission.id });

    request$.subscribe({
      next: () => {
        this.notification.success(assigned ? 'Permission removed.' : 'Permission assigned.');
        this.refreshRole(role.id);
        this.saving.set(false);
      },
      error: () => {
        this.notification.error('Failed to update permission.');
        this.saving.set(false);
      },
    });
  }

  private refreshRole(id: string): void {
    this.roleService.getById(id).subscribe({ next: (r) => this.role.set(r) });
  }
}
