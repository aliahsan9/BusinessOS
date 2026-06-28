import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../../components/app-button/app-button.component';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-forbidden-page',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './forbidden-page.component.html',
  styleUrl: './forbidden-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForbiddenPageComponent {
  readonly routes = ROUTES;
  readonly ButtonVariant = ButtonVariant;
}
