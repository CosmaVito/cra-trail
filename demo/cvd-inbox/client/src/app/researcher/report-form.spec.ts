import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { clickButton, element, settle, textOf, tick, typeInto } from '../../testing/dom';
import { ReportForm } from './report-form';

describe('ReportForm', () => {
  let fixture: ComponentFixture<ReportForm>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportForm],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ReportForm);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function fillIn(): void {
    typeInto(fixture, 'product', 'Example Product');
    typeInto(fixture, 'version', '1.2.3');
    typeInto(fixture, 'description', 'Stored XSS in the comment field of the admin page.');
  }

  it('shows what is missing and sends nothing', async () => {
    clickButton(fixture, 'Send report');
    await fixture.whenStable();

    const errors = Array.from(element(fixture).querySelectorAll('.error')).map((e) => e.textContent?.trim());
    expect(errors).toEqual(['Enter the name of the product.', 'Enter the version you tested.', 'Describe the vulnerability.']);
    const product = element(fixture).querySelector('#product');
    expect(product?.getAttribute('aria-invalid')).toBe('true');
    expect(product?.getAttribute('aria-describedby')).toBe('product-error');
    http.expectNone('/api/reports');
  });

  it('sends the report and shows the receipt code', async () => {
    fillIn();
    clickButton(fixture, 'Send report');
    await tick();

    const request = http.expectOne('/api/reports');
    expect(request.request.body).toEqual({
      product: 'Example Product',
      version: '1.2.3',
      description: 'Stored XSS in the comment field of the admin page.',
      contact: '',
      website: '',
    });
    request.flush({ receipt: '0123-4567-89ab-cdef-0123-4567-89ab-cdef' });
    await settle(fixture);

    expect(textOf(fixture, '[data-testid=receipt-code]')).toBe('0123-4567-89ab-cdef-0123-4567-89ab-cdef');
  });

  it('keeps the trap field away from people, autofill and screen readers', () => {
    const trap = element(fixture).querySelector('#website');

    expect(trap?.closest('[aria-hidden=true]')).not.toBeNull();
    expect(trap?.getAttribute('tabindex')).toBe('-1');
    expect(trap?.getAttribute('autocomplete')).toBe('off');
    expect(element(fixture).querySelector('label[for=website]')?.textContent).toContain('Leave this field empty');
  });

  it('says so when the server refuses', async () => {
    fillIn();
    clickButton(fixture, 'Send report');
    await tick();
    http.expectOne('/api/reports').flush(null, { status: 429, statusText: 'Too Many Requests' });
    await settle(fixture);

    expect(textOf(fixture, '[role=alert]')).toContain('Too many attempts');
  });
});
