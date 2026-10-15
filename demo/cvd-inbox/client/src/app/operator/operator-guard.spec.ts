import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';
import { operatorGuard } from './operator-guard';
import { Session } from './session';

describe('operatorGuard', () => {
  async function guard(operator: string | null): Promise<unknown> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: Session, useValue: { ensureLoaded: () => Promise.resolve(), operator: () => operator } },
      ],
    });
    return TestBed.runInInjectionContext(() => operatorGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  }

  it('lets operators in', async () => {
    expect(await guard('alice')).toBe(true);
  });

  it('sends everyone else to the sign-in page', async () => {
    const result = await guard(null);

    expect(result instanceof UrlTree ? result.toString() : result).toBe('/operator/sign-in');
  });
});
