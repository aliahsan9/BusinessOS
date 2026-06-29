import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TenantUsageDto } from '../../../core/models/tenant.model';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';

@Component({
  selector: 'app-tenant-usage',
  standalone: true,
  imports: [AppCardComponent],
  templateUrl: './tenant-usage.component.html',
  styleUrl: './tenant-usage.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantUsageComponent {
  readonly usage = input.required<TenantUsageDto>();

  percent(current: number, max: number): number {
    if (max <= 0) return 0;
    return Math.min(100, Math.round((current / max) * 100));
  }
}
