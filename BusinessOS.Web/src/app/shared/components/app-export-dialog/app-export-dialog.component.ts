import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppButtonComponent } from '../app-button/app-button.component';
import { ButtonVariant } from '../../../core/enums';
import { ReportType } from '../../../core/models/report.model';

export type ExportFormat = 'pdf';

@Component({
  selector: 'app-export-dialog',
  standalone: true,
  imports: [AppButtonComponent],
  templateUrl: './app-export-dialog.component.html',
  styleUrl: './app-export-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppExportDialogComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly title = input('Export Report');
  readonly message = input('Choose a report to export as PDF.');
  readonly loading = input(false);
  readonly reportOptions = input<{ type: ReportType; label: string }[]>([]);

  readonly exportReport = output<ReportType>();
  readonly cancelled = output<void>();

  onExport(type: ReportType): void {
    this.exportReport.emit(type);
  }

  onCancel(): void {
    this.cancelled.emit();
  }
}
