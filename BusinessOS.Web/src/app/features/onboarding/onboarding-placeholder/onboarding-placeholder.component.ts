import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-onboarding-placeholder',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './onboarding-placeholder.component.html',
  styleUrl: './onboarding-placeholder.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OnboardingPlaceholderComponent {
  readonly routes = ROUTES;
}
