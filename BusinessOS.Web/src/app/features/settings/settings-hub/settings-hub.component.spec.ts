import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SettingsHubComponent } from './settings-hub.component';

describe('SettingsHubComponent', () => {
  let fixture: ComponentFixture<SettingsHubComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsHubComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(SettingsHubComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
