import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { clickButton, element, settle, textOf, tick } from '../testing/dom';
import { App } from './app';
import { Session } from './operator/session';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('offers a skip link and the main navigation', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    expect(element(fixture).querySelector('a.skip-link')?.getAttribute('href')).toBe('#main');
    const links = Array.from(element(fixture).querySelectorAll('nav a')).map((a) => [a.textContent?.trim(), a.getAttribute('href')]);
    expect(links).toEqual([
      ['Report', '/'],
      ['Check status', '/status'],
      ['Security policy', '/security-policy'],
      ['Operator', '/operator'],
    ]);
  });

  it('says it is an example app', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    expect(textOf(fixture, 'footer')).toContain('not meant for production use');
  });

  it('shows who is signed in', async () => {
    const http = TestBed.inject(HttpTestingController);
    const loading = TestBed.inject(Session).ensureLoaded();
    http.expectOne('/api/auth/session').flush({ operator: 'alice' });
    await loading;

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    expect(textOf(fixture, '.signed-in')).toContain('Signed in as alice');
  });

  it('goes to the sign-in page after signing out, also when the session has expired', async () => {
    const http = TestBed.inject(HttpTestingController);
    const loading = TestBed.inject(Session).ensureLoaded();
    http.expectOne('/api/auth/session').flush({ operator: 'alice' });
    await loading;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    clickButton(fixture, 'Sign out');
    await tick();
    http.expectOne('/api/auth/sign-out').flush(null, { status: 401, statusText: 'Unauthorized' });
    await tick();
    http.expectOne('/api/auth/session').flush({ operator: null });
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith('/operator/sign-in');
    expect(element(fixture).querySelector('.signed-in')).toBeNull();
  });
});
