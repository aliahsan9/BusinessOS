import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyEmailComponent {
  readonly routes = ROUTES;
  readonly ButtonVariant = ButtonVariant;
}
