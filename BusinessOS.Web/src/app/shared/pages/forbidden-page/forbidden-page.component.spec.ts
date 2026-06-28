import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ForbiddenPageComponent } from './forbidden-page.component';

describe('ForbiddenPageComponent', () => {
  let component: ForbiddenPageComponent;
  let fixture: ComponentFixture<ForbiddenPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ForbiddenPageComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(ForbiddenPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should expose route constants', () => {
    expect(component.routes.dashboard).toBe('/dashboard');
    expect(component.routes.auth.login).toBe('/auth/login');
  });
});
