import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ProfitLossComponent } from './profit-loss.component';

describe('ProfitLossComponent', () => {
  let fixture: ComponentFixture<ProfitLossComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProfitLossComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(ProfitLossComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
