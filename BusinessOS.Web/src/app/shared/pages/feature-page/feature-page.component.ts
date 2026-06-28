import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AppBreadcrumbComponent } from '../../components/app-breadcrumb/app-breadcrumb.component';
import { AppCardComponent } from '../../components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../components/app-empty-state/app-empty-state.component';
import { APP_ROUTE_PATHS } from '../../constants/nav.constants';

export interface FeaturePageData {
  pageTitle: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-feature-page',
  standalone: true,
  imports: [AppBreadcrumbComponent, AppCardComponent, AppEmptyStateComponent],
  templateUrl: './feature-page.component.html',
  styleUrl: './feature-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeaturePageComponent {
  private readonly route = inject(ActivatedRoute);

  readonly pageData = this.route.snapshot.data as FeaturePageData;

  readonly breadcrumbs = [
    { label: 'Dashboard', route: APP_ROUTE_PATHS.dashboard },
    { label: this.pageData.pageTitle },
  ];
}
