import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppToastContainerComponent } from './app-toast-container.component';

describe('AppToastContainerComponent', () => {
  let fixture: ComponentFixture<AppToastContainerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppToastContainerComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppToastContainerComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
