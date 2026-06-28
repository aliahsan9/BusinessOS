import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppChartComponent } from './app-chart.component';

describe('AppChartComponent', () => {
  let fixture: ComponentFixture<AppChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppChartComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppChartComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
