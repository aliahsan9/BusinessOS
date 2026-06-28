import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppEmptyStateComponent } from './app-empty-state.component';

describe('AppEmptyStateComponent', () => {
  let fixture: ComponentFixture<AppEmptyStateComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppEmptyStateComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppEmptyStateComponent);
    fixture.componentRef.setInput('title', 'No data');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
