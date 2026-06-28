import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppButtonComponent } from '../../components/app-button/app-button.component';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-not-found-page',
  standalone: true,
  imports: [RouterLink, AppButtonComponent],
  templateUrl: './not-found-page.component.html',
  styleUrl: './not-found-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundPageComponent {
  readonly routes = ROUTES;
}
