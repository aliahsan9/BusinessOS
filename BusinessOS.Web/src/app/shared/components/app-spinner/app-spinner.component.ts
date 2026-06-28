import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-spinner',
  standalone: true,
  templateUrl: './app-spinner.component.html',
  styleUrl: './app-spinner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppSpinnerComponent {
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly label = input('Loading...');
}
