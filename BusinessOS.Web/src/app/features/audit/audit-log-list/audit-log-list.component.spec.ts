import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditLogListComponent } from './audit-log-list.component';

describe('AuditLogListComponent', () => {
  let fixture: ComponentFixture<AuditLogListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuditLogListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(AuditLogListComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
