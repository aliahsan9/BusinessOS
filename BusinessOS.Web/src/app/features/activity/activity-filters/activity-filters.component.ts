import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ACTIVITY_DATE_PRESETS,
  ACTIVITY_ENTITY_TYPES,
  ActivityDatePreset,
  ActivityEntityType,
} from '../../../core/models/activity.model';

@Component({
  selector: 'app-activity-filters',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './activity-filters.component.html',
  styleUrl: './activity-filters.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivityFiltersComponent {
  readonly search = input('');
  readonly entityType = input<ActivityEntityType | ''>('');
  readonly datePreset = input<ActivityDatePreset>('last7');
  readonly dateFrom = input('');
  readonly dateTo = input('');

  readonly searchChange = output<string>();
  readonly entityTypeChange = output<ActivityEntityType | ''>();
  readonly datePresetChange = output<ActivityDatePreset>();
  readonly dateFromChange = output<string>();
  readonly dateToChange = output<string>();
  readonly apply = output<void>();

  readonly entityTypes = ACTIVITY_ENTITY_TYPES;
  readonly datePresets = ACTIVITY_DATE_PRESETS;

  onSearchInput(value: string): void {
    this.searchChange.emit(value);
  }

  onEntityTypeChange(value: string): void {
    this.entityTypeChange.emit(value as ActivityEntityType | '');
  }

  onDatePresetChange(value: string): void {
    this.datePresetChange.emit(value as ActivityDatePreset);
  }
}
