import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppBadgeComponent } from './app-badge.component';

describe('AppBadgeComponent', () => {
  let fixture: ComponentFixture<AppBadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppBadgeComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppBadgeComponent);
    fixture.componentRef.setInput('label', 'Active');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
