import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AnalyticsPeriod } from '../../../core/enums';
import { ButtonVariant } from '../../../core/enums';
import { ReportService } from '../../../core/services/report.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  REPORT_DEFINITIONS,
  ReportDefinition,
  ReportHistoryItem,
  ReportQueryParams,
  ReportType,
} from '../../../core/models/report.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppDateRangePickerComponent, DateRangeSelection } from '../../../shared/components/app-date-range-picker/app-date-range-picker.component';

@Component({
  selector: 'app-report-hub',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    AppEmptyStateComponent,
    AppDateRangePickerComponent,
  ],
  templateUrl: './report-hub.component.html',
  styleUrl: './report-hub.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportHubComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly reportService = inject(ReportService);
  private readonly notification = inject(NotificationService);

  readonly definitions = REPORT_DEFINITIONS;
  readonly routes = ROUTES;
  readonly history = signal<ReportHistoryItem[]>([]);
  readonly loadingHistory = signal(true);
  readonly generatingType = signal<ReportType | null>(null);
  readonly regeneratingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly projectId = signal('');

  readonly period = signal<AnalyticsPeriod>(AnalyticsPeriod.Last30Days);
  readonly startDate = signal('');
  readonly endDate = signal('');

  readonly breadcrumbs = [
    { label: 'Dashboard', route: '/dashboard' },
    { label: 'Reports' },
  ];

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loadingHistory.set(true);
    this.reportService.getHistory().subscribe({
      next: (response) => {
        this.history.set(response.items);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.error.set('Failed to load report history.');
        this.loadingHistory.set(false);
      },
    });
  }

  onDateRangeChange(selection: DateRangeSelection): void {
    this.period.set(selection.period);
    this.startDate.set(selection.startDate ?? '');
    this.endDate.set(selection.endDate ?? '');
  }

  generateReport(definition: ReportDefinition): void {
    if (definition.requiresProjectId && !this.projectId().trim()) {
      this.notification.warning('Enter a project ID to generate this report.');
      return;
    }

    this.generatingType.set(definition.type);
    this.reportService.generateReport(definition.type, this.buildParams(definition)).subscribe({
      next: () => {
        this.notification.success(`${definition.label} downloaded successfully.`);
        this.generatingType.set(null);
        this.loadHistory();
      },
      error: () => {
        this.notification.error(`Failed to generate ${definition.label}.`);
        this.generatingType.set(null);
      },
    });
  }

  downloadReport(item: ReportHistoryItem): void {
    this.regeneratingId.set(item.id);
    this.reportService.downloadHistory(item.id).subscribe({
      next: () => {
        this.notification.success('Report downloaded.');
        this.regeneratingId.set(null);
      },
      error: () => {
        this.notification.error('Failed to download report.');
        this.regeneratingId.set(null);
      },
    });
  }

  regenerateReport(item: ReportHistoryItem): void {
    this.regeneratingId.set(item.id);
    this.reportService.regenerateHistory(item.id).subscribe({
      next: () => {
        this.notification.success('Report regenerated and downloaded.');
        this.regeneratingId.set(null);
        this.loadHistory();
      },
      error: () => {
        this.notification.error('Failed to regenerate report.');
        this.regeneratingId.set(null);
      },
    });
  }

  isGenerating(type: ReportType): boolean {
    return this.generatingType() === type;
  }

  isRowLoading(id: string): boolean {
    return this.regeneratingId() === id;
  }

  private buildParams(definition: ReportDefinition): ReportQueryParams {
    const params: ReportQueryParams = { period: this.period() };

    if (this.period() === AnalyticsPeriod.Custom) {
      params.startDate = this.startDate();
      params.endDate = this.endDate();
    }

    if (definition.requiresProjectId) {
      params.projectId = this.projectId().trim();
    }

    return params;
  }
}
