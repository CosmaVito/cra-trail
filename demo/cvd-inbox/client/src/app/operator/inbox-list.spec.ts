import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReportSummary } from '../api/models';
import { element, settle, textOf, tick } from '../../testing/dom';
import { InboxList } from './inbox-list';

describe('InboxList', () => {
  let fixture: ComponentFixture<InboxList>;
  let http: HttpTestingController;
  const summary: ReportSummary = {
    id: 7,
    product: 'Example Product',
    version: '1.2.3',
    status: 'new',
    receivedAt: '2026-10-15T09:00:00Z',
    updatedAt: '2026-10-15T09:00:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InboxList],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InboxList);
  });

  afterEach(() => http.verify());

  it('lists reports with a link to each one', async () => {
    http.expectOne('/api/inbox').flush([summary]);
    await settle(fixture);

    const link = element(fixture).querySelector('tbody a');
    expect(link?.textContent?.trim()).toBe('Example Product');
    expect(link?.getAttribute('href')).toBe('/operator/reports/7');
    expect(textOf(fixture, 'tbody td:last-child')).toBe('New');
  });

  it('asks the server for one status when filtered', async () => {
    http.expectOne('/api/inbox').flush([summary]);
    await settle(fixture);

    const select = element(fixture).querySelector<HTMLSelectElement>('#status-filter');
    select!.value = 'triage';
    select!.dispatchEvent(new Event('change'));
    await tick();
    http.expectOne((request) => request.url === '/api/inbox' && request.params.get('status') === 'triage').flush([]);
    await settle(fixture);

    expect(textOf(fixture, '.empty')).toBe('No reports.');
  });
});
