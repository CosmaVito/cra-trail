import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { ReportDetail } from '../api/models';
import { clickButton, element, settle, textOf, tick, typeInto } from '../../testing/dom';
import { ReportView } from './report-view';

describe('ReportView', () => {
  let fixture: ComponentFixture<ReportView>;
  let http: HttpTestingController;
  const detail: ReportDetail = {
    id: 7,
    product: 'Example Product',
    version: '1.2.3',
    status: 'new',
    receivedAt: '2026-10-15T09:00:00Z',
    updatedAt: '2026-10-15T09:00:00Z',
    description: '<img src=x onerror="alert(1)">',
    contact: 'researcher@example.test',
    contactErasedAt: null,
    closedAt: null,
    nextStatuses: ['triage', 'rejected'],
    notes: [],
  };

  async function open(report: ReportDetail): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ReportView],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ReportView);
    http.expectOne('/api/inbox/7').flush(report);
    await settle(fixture);
  }

  afterEach(() => {
    (fixture?.nativeElement as HTMLElement | undefined)?.remove();
    http.verify();
  });

  it('shows the description as text, never as markup', async () => {
    await open(detail);

    expect(element(fixture).querySelector('img')).toBeNull();
    expect(textOf(fixture, '[data-testid=description]')).toBe('<img src=x onerror="alert(1)">');
  });

  it('offers only the moves the workflow allows', async () => {
    await open(detail);

    const buttons = Array.from(element(fixture).querySelectorAll('.actions button')).map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Move to Triage', 'Move to Rejected']);
  });

  it('moves the report and shows the new status', async () => {
    await open(detail);

    clickButton(fixture, 'Move to Triage');
    await tick();
    const request = http.expectOne('/api/inbox/7/status');
    expect(request.request.body).toEqual({ status: 'triage' });
    request.flush({ ...detail, status: 'triage', nextStatuses: ['accepted', 'rejected'] });
    await settle(fixture);

    expect(textOf(fixture, '[data-testid=report-status]')).toBe('Triage');
  });

  it('says when the contact was erased', async () => {
    await open({ ...detail, contact: null, contactErasedAt: '2026-11-20T09:00:00Z' });

    expect(textOf(fixture, '[data-testid=contact]')).toBe('Erased on 20 Nov 2026');
  });

  it('asks before deleting for good', async () => {
    await open(detail);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    clickButton(fixture, 'Delete report');
    await fixture.whenStable();
    http.expectNone({ method: 'DELETE', url: '/api/inbox/7' });
    clickButton(fixture, 'Delete for good');
    await tick();
    http.expectOne({ method: 'DELETE', url: '/api/inbox/7' }).flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith('/operator');
  });

  it('moves focus to the note after an invalid submit', async () => {
    await open(detail);
    document.body.appendChild(element(fixture));

    typeInto(fixture, 'note', '   ');
    clickButton(fixture, 'Add note');
    await fixture.whenStable();

    expect(textOf(fixture, '#note-error')).toBe('Write the note first.');
    expect(document.activeElement?.id).toBe('note');
  });

  it('shows a server field error on the note text and focuses it', async () => {
    await open(detail);
    document.body.appendChild(element(fixture));

    typeInto(fixture, 'note', 'Looks real.');
    clickButton(fixture, 'Add note');
    await tick();
    http.expectOne('/api/inbox/7/notes').flush(
      { title: 'One or more validation errors occurred.', errors: { text: ['Remove the hidden character U+202E.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle(fixture);

    const note = element(fixture).querySelector('#note');
    expect(textOf(fixture, '#note-error')).toBe('Remove the hidden character U+202E.');
    expect(note?.getAttribute('aria-invalid')).toBe('true');
    expect(note?.getAttribute('aria-describedby')).toBe('note-hint note-error');
    expect(document.activeElement).toBe(note);
    expect(element(fixture).querySelector('[role=alert]')).toBeNull();
  });

  it('adds a note and clears the field', async () => {
    await open(detail);

    typeInto(fixture, 'note', 'Looks real.');
    clickButton(fixture, 'Add note');
    await tick();
    const request = http.expectOne('/api/inbox/7/notes');
    expect(request.request.body).toEqual({ text: 'Looks real.' });
    request.flush({ id: 1, author: 'alice', text: 'Looks real.', createdAt: '2026-10-15T10:00:00Z' });
    await settle(fixture);

    expect(textOf(fixture, '.note-text')).toBe('Looks real.');
    expect(element(fixture).querySelector<HTMLTextAreaElement>('#note')?.value).toBe('');
  });

  it('announces the new status and moves focus to the status heading', async () => {
    await open(detail);
    document.body.appendChild(element(fixture));

    clickButton(fixture, 'Move to Triage');
    await tick();
    http.expectOne('/api/inbox/7/status').flush({ ...detail, status: 'triage', nextStatuses: ['accepted', 'rejected'] });
    await settle(fixture);

    expect(textOf(fixture, '[role=status]')).toBe('Status changed to Triage');
    expect(document.activeElement?.id).toBe('status-heading');
  });

  it('announces an added note', async () => {
    await open(detail);

    typeInto(fixture, 'note', 'Looks real.');
    clickButton(fixture, 'Add note');
    await tick();
    http.expectOne('/api/inbox/7/notes').flush({ id: 1, author: 'alice', text: 'Looks real.', createdAt: '2026-10-15T10:00:00Z' });
    await settle(fixture);

    expect(textOf(fixture, '[role=status]')).toBe('Note added');
  });

  it('reloads the report when the status changed under the operator', async () => {
    await open(detail);

    clickButton(fixture, 'Move to Triage');
    await tick();
    http.expectOne('/api/inbox/7/status').flush(
      { title: 'A report in Accepted cannot move to Triage.' },
      { status: 409, statusText: 'Conflict' },
    );
    await tick();
    http.expectOne('/api/inbox/7').flush({ ...detail, status: 'accepted', nextStatuses: ['disclosed'] });
    await settle(fixture);

    const buttons = Array.from(element(fixture).querySelectorAll('.actions button')).map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Move to Disclosed']);
    expect(textOf(fixture, '[data-testid=report-status]')).toBe('Accepted');
    expect(textOf(fixture, '[role=alert]')).toBe('A report in Accepted cannot move to Triage.');
  });

  it('moves focus to the status heading after the reload', async () => {
    await open(detail);
    document.body.appendChild(element(fixture));

    clickButton(fixture, 'Move to Triage');
    await tick();
    http.expectOne('/api/inbox/7/status').flush(
      { title: 'Another operator changed this report meanwhile. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await tick();
    http.expectOne('/api/inbox/7').flush({ ...detail, status: 'triage', nextStatuses: ['accepted', 'rejected'] });
    await settle(fixture);

    expect(document.activeElement?.id).toBe('status-heading');
  });

  it('keeps the conflict message when the reload fails', async () => {
    await open(detail);

    clickButton(fixture, 'Move to Triage');
    await tick();
    http.expectOne('/api/inbox/7/status').flush(
      { title: 'Another operator changed this report meanwhile. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await tick();
    http.expectOne('/api/inbox/7').flush(null, { status: 500, statusText: 'Server Error' });
    await settle(fixture);

    expect(textOf(fixture, '[role=alert]')).toBe('Another operator changed this report meanwhile. Reload it and try again.');
  });

  it('drops the page when the report is gone', async () => {
    await open(detail);

    clickButton(fixture, 'Move to Triage');
    await tick();
    http.expectOne('/api/inbox/7/status').flush({ title: 'No such report.' }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);

    expect(element(fixture).querySelectorAll('button')).toHaveLength(0);
    expect(textOf(fixture, '[role=alert]')).toBe('This report does not exist. It may have been deleted.');
  });

  it('moves focus into the delete confirmation and back on cancel', async () => {
    await open(detail);
    document.body.appendChild(element(fixture));

    clickButton(fixture, 'Delete report');
    await fixture.whenStable();
    expect(document.activeElement?.textContent?.trim()).toBe('Delete for good');

    clickButton(fixture, 'Cancel');
    await fixture.whenStable();
    expect(document.activeElement?.textContent?.trim()).toBe('Delete report');
  });
});
