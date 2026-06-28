import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  templateUrl: './app-skeleton.component.html',
  styleUrl: './app-skeleton.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppSkeletonComponent {
  readonly width = input<string>('100%');
  readonly height = input<string>('1rem');
  readonly variant = input<'text' | 'rect' | 'circle'>('text');
}
