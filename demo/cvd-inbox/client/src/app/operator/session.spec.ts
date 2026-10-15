import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { tick } from '../../testing/dom';
import { Session } from './session';

describe('Session', () => {
  let session: Session;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    session = TestBed.inject(Session);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the session once', async () => {
    const first = session.ensureLoaded();
    const second = session.ensureLoaded();
    http.expectOne('/api/auth/session').flush({ operator: 'alice' });
    await Promise.all([first, second]);

    expect(session.operator()).toBe('alice');
  });

  it('treats a failed session call as signed out', async () => {
    const loading = session.ensureLoaded();
    http.expectOne('/api/auth/session').flush(null, { status: 500, statusText: 'Server Error' });
    await loading;

    expect(session.operator()).toBeNull();
  });

  it('fetches a new session, and with it a new request token, after signing in', async () => {
    const signingIn = session.signIn('alice', 'correct horse battery staple');
    const signIn = http.expectOne('/api/auth/sign-in');
    expect(signIn.request.body).toEqual({ name: 'alice', password: 'correct horse battery staple' });
    signIn.flush(null, { status: 204, statusText: 'No Content' });
    await tick();
    http.expectOne('/api/auth/session').flush({ operator: 'alice' });
    await signingIn;

    expect(session.operator()).toBe('alice');
  });
});
