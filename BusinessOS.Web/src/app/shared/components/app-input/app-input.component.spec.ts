import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppInputComponent } from './app-input.component';

describe('AppInputComponent', () => {
  let component: AppInputComponent;
  let fixture: ComponentFixture<AppInputComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppInputComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AppInputComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should implement ControlValueAccessor', () => {
    const onChange = jasmine.createSpy('onChange');
    component.registerOnChange(onChange);
    component.onInput({ target: { value: 'test' } } as unknown as Event);
    expect(onChange).toHaveBeenCalledWith('test');
  });
});
