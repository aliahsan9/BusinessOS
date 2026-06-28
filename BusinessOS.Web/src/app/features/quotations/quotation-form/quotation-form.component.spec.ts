import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { QuotationFormComponent } from './quotation-form.component';

describe('QuotationFormComponent', () => {
  let fixture: ComponentFixture<QuotationFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuotationFormComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(QuotationFormComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
