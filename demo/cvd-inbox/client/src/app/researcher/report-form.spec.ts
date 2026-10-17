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

  afterEach(() => {
    (fixture?.nativeElement as HTMLElement | undefined)?.remove();
    http.verify();
  });

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

    expect(textOf(fixture, '[role=alert]')).toBe('Too many attempts from your network. Wait a while and try again.');
  });

  it('moves focus to the message when the server refuses', async () => {
    document.body.appendChild(element(fixture));
    fillIn();
    clickButton(fixture, 'Send report');
    await tick();
    http.expectOne('/api/reports').flush(null, { status: 500, statusText: 'Server Error' });
    await settle(fixture);

    expect(document.activeElement).toBe(element(fixture).querySelector('[role=alert]'));
  });

  it('counts the product without its surrounding spaces', async () => {
    fillIn();
    typeInto(fixture, 'product', 'p'.repeat(100) + '   ');
    clickButton(fixture, 'Send report');
    await tick();

    const request = http.expectOne('/api/reports');
    expect(request.request.body.product).toBe('p'.repeat(100) + '   ');
    request.flush({ receipt: '0123-4567-89ab-cdef-0123-4567-89ab-cdef' });
    await settle(fixture);
  });

  it('drops a server field error once the field changes', async () => {
    fillIn();
    clickButton(fixture, 'Send report');
    await tick();
    http.expectOne('/api/reports').flush(
      { title: 'One or more validation errors occurred.', errors: { description: ['Remove the hidden character U+202E.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle(fixture);
    expect(textOf(fixture, '#description-error')).toBe('Remove the hidden character U+202E.');

    typeInto(fixture, 'description', 'Stored XSS in the comment field, typed again.');
    await fixture.whenStable();

    expect(element(fixture).querySelector('#description-error')).toBeNull();
  });

  it('refuses a product that is only spaces', async () => {
    typeInto(fixture, 'product', '    ');
    typeInto(fixture, 'version', '1.2.3');
    typeInto(fixture, 'description', 'Stored XSS in the comment field of the admin page.');
    clickButton(fixture, 'Send report');
    await fixture.whenStable();

    expect(textOf(fixture, '#product-error')).toBe('Enter the name of the product.');
    http.expectNone('/api/reports');
  });

  it('counts the description without its surrounding spaces', async () => {
    fillIn();
    typeInto(fixture, 'description', 'x' + ' '.repeat(25));
    clickButton(fixture, 'Send report');
    await fixture.whenStable();

    expect(textOf(fixture, '#description-error')).toBe('Write at least 20 characters.');
    http.expectNone('/api/reports');
  });

  it('moves focus to the first invalid field after an invalid submit', async () => {
    document.body.appendChild(element(fixture));
    clickButton(fixture, 'Send report');
    await fixture.whenStable();

    expect(document.activeElement?.id).toBe('product');
  });

  it('moves focus to the receipt heading after a successful submit', async () => {
    document.body.appendChild(element(fixture));
    fillIn();
    clickButton(fixture, 'Send report');
    await tick();
    http.expectOne('/api/reports').flush({ receipt: '0123-4567-89ab-cdef-0123-4567-89ab-cdef' });
    await settle(fixture);

    expect(document.activeElement?.id).toBe('receipt-heading');
    expect(textOf(fixture, '#receipt-heading')).toBe('Report received');
  });

  it('shows a server field error next to its field and focuses it', async () => {
    document.body.appendChild(element(fixture));
    fillIn();
    clickButton(fixture, 'Send report');
    await tick();
    http.expectOne('/api/reports').flush(
      { title: 'One or more validation errors occurred.', errors: { description: ['Remove the hidden character U+202E.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle(fixture);

    const description = element(fixture).querySelector('#description');
    expect(textOf(fixture, '#description-error')).toBe('Remove the hidden character U+202E.');
    expect(description?.getAttribute('aria-invalid')).toBe('true');
    expect(description?.getAttribute('aria-describedby')).toBe('description-hint description-error');
    expect(document.activeElement).toBe(description);
    expect(element(fixture).querySelector('[role=alert]')).toBeNull();
  });

  it('exposes which fields are required', () => {
    const root = element(fixture);

    for (const id of ['product', 'version', 'description']) {
      expect(root.querySelector(`#${id}`)?.getAttribute('aria-required')).toBe('true');
    }
    expect(root.querySelector('#contact')?.hasAttribute('aria-required')).toBe(false);
  });
});
