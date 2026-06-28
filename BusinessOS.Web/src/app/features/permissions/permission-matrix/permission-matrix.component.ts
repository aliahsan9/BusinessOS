import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { PermissionService } from '../../../core/services/permission.service';
import { PermissionDto } from '../../../core/models/role.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-permission-matrix',
  standalone: true,
  imports: [
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './permission-matrix.component.html',
  styleUrl: './permission-matrix.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PermissionMatrixComponent implements OnInit {
  private readonly permissionService = inject(PermissionService);

  readonly permissions = signal<PermissionDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly routes = ROUTES;
  readonly breadcrumbs = [{ label: 'Permissions', route: ROUTES.permissions.list }];

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
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.permissionService.getAll().subscribe({
      next: (items) => {
        this.permissions.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load permissions.');
        this.loading.set(false);
      },
    });
  }
}
