import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppPaginationComponent } from './app-pagination.component';

describe('AppPaginationComponent', () => {
  let component: AppPaginationComponent;
  let fixture: ComponentFixture<AppPaginationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AppPaginationComponent] }).compileComponents();
    fixture = TestBed.createComponent(AppPaginationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('page', 1);
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('totalCount', 100);
    fixture.componentRef.setInput('totalPages', 5);
    fixture.detectChanges();
  });

  it('should emit page change', () => {
    const spy = jasmine.createSpy('pageChange');
    component.pageChange.subscribe(spy);
    component.goTo(2);
    expect(spy).toHaveBeenCalledWith(2);
  });
});
