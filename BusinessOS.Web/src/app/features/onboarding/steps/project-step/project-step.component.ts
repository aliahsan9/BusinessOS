import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../../../../shared/components/app-button/app-button.component';
import { ButtonVariant } from '../../../../core/enums';
import { ROUTES } from '../../../../core/constants/route.constants';

@Component({
  selector: 'app-project-step',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './project-step.component.html',
  styleUrl: '../guided-step/guided-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectStepComponent {
  readonly ButtonVariant = ButtonVariant;
  readonly routes = ROUTES;
  readonly next = output<void>();
  readonly back = output<void>();
  readonly skipStep = output<void>();
}
