import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { InventoryDashboardComponent } from './inventory-dashboard.component';
import { StockTransactionType } from '../../../core/enums';

describe('InventoryDashboardComponent', () => {
  let fixture: ComponentFixture<InventoryDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InventoryDashboardComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(InventoryDashboardComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should map transaction type variants', () => {
    expect(fixture.componentInstance.getTypeVariant(StockTransactionType.Purchase)).toBe('success');
  });
});
