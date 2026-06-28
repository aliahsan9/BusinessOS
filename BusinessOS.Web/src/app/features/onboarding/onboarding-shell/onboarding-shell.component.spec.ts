import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OnboardingShellComponent } from './onboarding-shell.component';

describe('OnboardingShellComponent', () => {
  let component: OnboardingShellComponent;
  let fixture: ComponentFixture<OnboardingShellComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OnboardingShellComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(OnboardingShellComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
