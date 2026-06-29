import { CurrencyPipe } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';
import { CurrencyService } from '../../core/services/currency.service';

@Pipe({
  name: 'appCurrency',
  standalone: true,
  pure: false,
})
export class AppCurrencyPipe implements PipeTransform {
  private readonly currencyService = inject(CurrencyService);

  transform(
    value: number | string | null | undefined,
    display?: 'code' | 'symbol' | 'symbol-narrow' | string | boolean,
    digitsInfo?: string,
    locale?: string,
  ): string | null {
    const resolvedLocale = locale ?? this.currencyService.locale();
    const currencyPipe = new CurrencyPipe(resolvedLocale);

    return currencyPipe.transform(
      value,
      this.currencyService.currencyCode(),
      display === undefined || display === false ? 'symbol' : display,
      digitsInfo ?? '1.2-2',
    );
  }
}
