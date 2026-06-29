import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { AnalyticsPeriod } from '../../../core/enums';
import { AppButtonComponent } from '../app-button/app-button.component';
import { ButtonVariant } from '../../../core/enums';

export interface DateRangeSelection {
  period: AnalyticsPeriod;
  startDate?: string;
  endDate?: string;
}

@Component({
  selector: 'app-date-range-picker',
  standalone: true,
  imports: [AppButtonComponent],
  templateUrl: './app-date-range-picker.component.html',
  styleUrl: './app-date-range-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppDateRangePickerComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly label = input('Date range');
  readonly period = input<AnalyticsPeriod>(AnalyticsPeriod.Last30Days);
  readonly startDate = input('');
  readonly endDate = input('');

  readonly periodChange = output<AnalyticsPeriod>();
  readonly rangeApply = output<DateRangeSelection>();

  readonly showCustom = signal(false);
  readonly localStart = signal('');
  readonly localEnd = signal('');

  readonly periods = [
    { label: 'Last 7 Days', value: AnalyticsPeriod.Last7Days },
    { label: 'Last 30 Days', value: AnalyticsPeriod.Last30Days },
    { label: 'Last 90 Days', value: AnalyticsPeriod.Last90Days },
    { label: 'This Year', value: AnalyticsPeriod.Year },
    { label: 'Custom Range', value: AnalyticsPeriod.Custom },
  ];

  onPeriodChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as AnalyticsPeriod;
    if (value === AnalyticsPeriod.Custom) {
      this.showCustom.set(true);
      this.localStart.set(this.startDate());
      this.localEnd.set(this.endDate());
      return;
    }
    this.showCustom.set(false);
    this.periodChange.emit(value);
    this.rangeApply.emit({ period: value });
  }

  applyCustomRange(): void {
    const start = this.localStart();
    const end = this.localEnd();
    if (!start || !end) return;
    this.rangeApply.emit({ period: AnalyticsPeriod.Custom, startDate: start, endDate: end });
  }
}
