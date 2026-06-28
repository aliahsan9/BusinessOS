import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { InventoryStateService } from '../../../state/inventory.state';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

interface OverviewNavCard {
  title: string;
  description: string;
  route: string;
  icon: string;
}

@Component({
  selector: 'app-inventory-overview',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    DecimalPipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
  ],
  templateUrl: './inventory-overview.component.html',
  styleUrl: './inventory-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryOverviewComponent implements OnInit {
  private readonly inventoryState = inject(InventoryStateService);

  readonly analytics = this.inventoryState.analytics;
  readonly analyticsLoading = this.inventoryState.analyticsLoadingState;
  readonly routes = ROUTES;

  readonly breadcrumbs = [
    { label: 'Inventory', route: ROUTES.inventory.overview },
    { label: 'Overview' },
  ];

  readonly navCards: OverviewNavCard[] = [
    {
      title: 'Stock Levels',
      description: 'View and adjust current stock for all products.',
      route: ROUTES.inventory.stockLevels,
      icon: '📊',
    },
    {
      title: 'Stock History',
      description: 'Browse stock transactions and audit trail.',
      route: ROUTES.inventory.history,
      icon: '📜',
    },
    {
      title: 'Reports',
      description: 'Inventory value and profit potential analysis.',
      route: ROUTES.inventory.reports,
      icon: '📈',
    },
    {
      title: 'Dashboard',
      description: 'Visual overview of inventory KPIs and trends.',
      route: ROUTES.inventory.dashboard,
      icon: '🏭',
    },
  ];

  ngOnInit(): void {
    this.inventoryState.loadAnalytics();
  }

  retry(): void {
    this.inventoryState.loadAnalytics();
  }
}
