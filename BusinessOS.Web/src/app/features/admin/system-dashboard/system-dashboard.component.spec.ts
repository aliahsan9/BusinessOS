import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SystemDashboardComponent } from './system-dashboard.component';

describe('SystemDashboardComponent', () => {
  let fixture: ComponentFixture<SystemDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SystemDashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(SystemDashboardComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
