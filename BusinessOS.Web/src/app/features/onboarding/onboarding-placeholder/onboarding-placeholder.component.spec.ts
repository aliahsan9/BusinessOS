import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OnboardingPlaceholderComponent } from './onboarding-placeholder.component';

describe('OnboardingPlaceholderComponent', () => {
  let component: OnboardingPlaceholderComponent;
  let fixture: ComponentFixture<OnboardingPlaceholderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OnboardingPlaceholderComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(OnboardingPlaceholderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
