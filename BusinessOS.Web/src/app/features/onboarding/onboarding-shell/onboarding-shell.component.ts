import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-onboarding-shell',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './onboarding-shell.component.html',
  styleUrl: './onboarding-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OnboardingShellComponent {}
