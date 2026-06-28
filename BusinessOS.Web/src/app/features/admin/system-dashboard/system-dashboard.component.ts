import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { SystemAdminService } from '../../../core/services/system-admin.service';
import { EnvironmentInfo, SystemHealth, SystemStats } from '../../../core/models/system.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-system-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
  ],
  templateUrl: './system-dashboard.component.html',
  styleUrl: './system-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SystemDashboardComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly systemAdminService = inject(SystemAdminService);

  readonly health = signal<SystemHealth | null>(null);
  readonly stats = signal<SystemStats | null>(null);
  readonly environment = signal<EnvironmentInfo | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly routes = ROUTES;
  readonly breadcrumbs = [{ label: 'System Admin', route: ROUTES.admin.dashboard }];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      health: this.systemAdminService.getHealth(),
      stats: this.systemAdminService.getStats(),
      environment: this.systemAdminService.getEnvironment(),
    }).subscribe({
      next: ({ health, stats, environment }) => {
        this.health.set(health);
        this.stats.set(stats);
        this.environment.set(environment);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load system dashboard.');
        this.loading.set(false);
      },
    });
  }

  healthVariant(status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Healthy: 'success',
      Degraded: 'warning',
      Unhealthy: 'danger',
    };
    return map[status] ?? 'neutral';
  }

  retry(): void {
    this.load();
  }
}
