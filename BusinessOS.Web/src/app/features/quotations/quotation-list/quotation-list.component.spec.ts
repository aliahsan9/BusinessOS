import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { QuotationListComponent } from './quotation-list.component';

describe('QuotationListComponent', () => {
  let fixture: ComponentFixture<QuotationListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuotationListComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(QuotationListComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
