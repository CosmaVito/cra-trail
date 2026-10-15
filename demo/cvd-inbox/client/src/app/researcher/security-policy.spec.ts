import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { element, settle, textOf } from '../../testing/dom';
import { SecurityPolicy } from './security-policy';

describe('SecurityPolicy', () => {
  let fixture: ComponentFixture<SecurityPolicy>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SecurityPolicy],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SecurityPolicy);
  });

  afterEach(() => http.verify());

  it('explains the process with the configured numbers', async () => {
    http.expectOne('/api/disclosure').flush({
      reportUrl: 'https://inbox.example.test/',
      contact: ['mailto:security@example.test'],
      expires: '2027-06-30T00:00:00Z',
      preferredLanguages: 'en',
      acknowledgeWithinDays: 5,
      disclosureAfterDays: 90,
      contactRetentionDays: 30,
    });
    await settle(fixture);

    const page = element(fixture).textContent ?? '';
    expect(page).toContain('within 5 days');
    expect(page).toContain('at most 90 days after your report');
    expect(page).toContain('we erase them 30 days after the report is closed');
    expect(element(fixture).querySelector('a[href="mailto:security@example.test"]')).not.toBeNull();
  });

  it('says so when the policy cannot be loaded', async () => {
    http.expectOne('/api/disclosure').flush(null, { status: 500, statusText: 'Server Error' });
    await settle(fixture);

    expect(textOf(fixture, '[role=alert]')).toBe('Something went wrong on the server. Try again later.');
  });
});
