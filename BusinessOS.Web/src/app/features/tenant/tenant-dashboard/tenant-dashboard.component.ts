import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TenantService } from '../../../core/services/tenant.service';
import { TenantDashboardDto } from '../../../core/models/tenant.model';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { TokenService } from '../../../core/services/token.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { TenantUsageComponent } from '../tenant-usage/tenant-usage.component';

@Component({
  selector: 'app-tenant-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    TenantUsageComponent,
  ],
  templateUrl: './tenant-dashboard.component.html',
  styleUrl: './tenant-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantDashboardComponent implements OnInit {
  readonly ROUTES = ROUTES;
  private readonly tenantService = inject(TenantService);
  private readonly tokenService = inject(TokenService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dashboard = signal<TenantDashboardDto | null>(null);
  readonly canManage = this.tokenService.hasPermission(PermissionCodes.tenant.manage);
  readonly breadcrumbs = [{ label: 'Tenant', route: ROUTES.tenant.dashboard }, { label: 'Dashboard' }];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.tenantService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load tenant dashboard.');
        this.loading.set(false);
      },
    });
  }
}
