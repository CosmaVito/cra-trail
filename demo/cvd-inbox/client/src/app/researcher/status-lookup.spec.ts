import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { clickButton, settle, textOf, tick, typeInto } from '../../testing/dom';
import { StatusLookup } from './status-lookup';

describe('StatusLookup', () => {
  let fixture: ComponentFixture<StatusLookup>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatusLookup],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(StatusLookup);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('looks up the code as typed and shows the status', async () => {
    typeInto(fixture, 'receipt', '0123 4567 89AB CDEF 0123 4567 89ab cdef');
    clickButton(fixture, 'Check status');
    await tick();

    const request = http.expectOne('/api/reports/status');
    expect(request.request.body).toEqual({ receipt: '0123 4567 89AB CDEF 0123 4567 89ab cdef' });
    request.flush({ status: 'triage', receivedAt: '2026-10-15T09:00:00Z', updatedAt: '2026-10-16T10:00:00Z' });
    await settle(fixture);

    expect(textOf(fixture, '[data-testid=report-status]')).toBe('Triage');
  });

  it('says when no report matches', async () => {
    typeInto(fixture, 'receipt', '0123456789abcdef0123456789abcdef');
    clickButton(fixture, 'Check status');
    await tick();
    http.expectOne('/api/reports/status').flush({ title: 'No report matches this receipt code.' }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);

    expect(textOf(fixture, '[role=alert]')).toBe('No report matches this receipt code. Check it and try again.');
  });

  it('checks the shape of the code before asking', async () => {
    typeInto(fixture, 'receipt', 'abc');
    clickButton(fixture, 'Check status');
    await fixture.whenStable();

    expect(textOf(fixture, '#receipt-error')).toBe('Receipt codes have 32 letters and digits, from 0-9 and a-f.');
    http.expectNone('/api/reports/status');
  });

  it('shows a server field error next to the receipt field', async () => {
    typeInto(fixture, 'receipt', '0123456789abcdef0123456789abcdef');
    clickButton(fixture, 'Check status');
    await tick();
    http.expectOne('/api/reports/status').flush(
      { title: 'One or more validation errors occurred.', errors: { receipt: ['Enter a valid receipt code.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle(fixture);

    expect(textOf(fixture, '#receipt-error')).toBe('Enter a valid receipt code.');
    expect(fixture.nativeElement.querySelector('#receipt').getAttribute('aria-invalid')).toBe('true');
    expect(fixture.nativeElement.querySelector('[role=alert]')).toBeNull();
  });
});
