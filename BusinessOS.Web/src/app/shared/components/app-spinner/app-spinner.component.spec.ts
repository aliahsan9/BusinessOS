import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppSpinnerComponent } from './app-spinner.component';

describe('AppSpinnerComponent', () => {
  let fixture: ComponentFixture<AppSpinnerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppSpinnerComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppSpinnerComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
