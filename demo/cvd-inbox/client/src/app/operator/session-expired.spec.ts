import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { Session } from './session';
import { sessionExpired } from './session-expired';

describe('sessionExpired', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionExpired])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  it('forgets the operator and goes to sign-in on a 401 from the inbox API', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const expire = vi.spyOn(TestBed.inject(Session), 'expire');

    const call = TestBed.inject(InboxApi).reports();
    http.expectOne('/api/inbox').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(expire).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/operator/sign-in');
  });

  it('leaves a refused sign-in to the sign-in page', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    const call = TestBed.inject(InboxApi).signIn('alice', 'wrong');
    http.expectOne('/api/auth/sign-in').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(navigate).not.toHaveBeenCalled();
  });
});
