import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppAlertComponent } from './app-alert.component';

describe('AppAlertComponent', () => {
  let fixture: ComponentFixture<AppAlertComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppAlertComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppAlertComponent);
    fixture.componentRef.setInput('message', 'Test');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
