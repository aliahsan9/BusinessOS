import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-search-bar',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app-search-bar.component.html',
  styleUrl: './app-search-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppSearchBarComponent {
  readonly placeholder = input('Search...');
  readonly value = input('');

  readonly search = output<string>();

  onInput(event: Event): void {
    this.search.emit((event.target as HTMLInputElement).value);
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    this.search.emit(this.value());
  }
}
