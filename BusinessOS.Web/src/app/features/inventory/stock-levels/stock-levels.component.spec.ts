import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { StockLevelsComponent } from './stock-levels.component';

describe('StockLevelsComponent', () => {
  let fixture: ComponentFixture<StockLevelsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StockLevelsComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(StockLevelsComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
