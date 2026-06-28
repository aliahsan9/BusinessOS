import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { StockHistoryComponent } from './stock-history.component';
import { StockTransactionType } from '../../../core/enums';

describe('StockHistoryComponent', () => {
  let fixture: ComponentFixture<StockHistoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StockHistoryComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(StockHistoryComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should map transaction type variants', () => {
    expect(fixture.componentInstance.getTypeVariant(StockTransactionType.Sale)).toBe('primary');
    expect(fixture.componentInstance.getTypeVariant(StockTransactionType.Damage)).toBe('danger');
  });
});
