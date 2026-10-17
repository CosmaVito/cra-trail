import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { clickButton, element, settle, textOf, typeInto } from '../../testing/dom';
import { Session } from './session';
import { SECURE_CONTEXT, SignIn } from './sign-in';

describe('SignIn', () => {
  let fixture: ComponentFixture<SignIn>;
  const session = { ensureLoaded: vi.fn(), signIn: vi.fn() };

  beforeEach(async () => {
    session.ensureLoaded.mockReset().mockResolvedValue(undefined);
    session.signIn.mockReset();
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [provideRouter([]), { provide: Session, useValue: session }, { provide: SECURE_CONTEXT, useValue: true }],
    }).compileComponents();
    fixture = TestBed.createComponent(SignIn);
    await fixture.whenStable();
  });

  afterEach(() => (fixture?.nativeElement as HTMLElement | undefined)?.remove());

  it('signs in and opens the inbox', async () => {
    session.signIn.mockResolvedValue(undefined);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    typeInto(fixture, 'name', 'alice');
    typeInto(fixture, 'password', 'correct horse battery staple');
    clickButton(fixture, 'Sign in');
    await settle(fixture);

    expect(session.signIn).toHaveBeenCalledWith('alice', 'correct horse battery staple');
    expect(navigate).toHaveBeenCalledWith('/operator');
    // The password leaves the form model as soon as it has been sent.
    expect(element(fixture).querySelector<HTMLInputElement>('#password')?.value).toBe('');
  });

  it('refuses a name of only spaces', async () => {
    typeInto(fixture, 'name', '   ');
    typeInto(fixture, 'password', 'correct horse battery staple');
    clickButton(fixture, 'Sign in');
    await fixture.whenStable();

    expect(textOf(fixture, '#name-error')).toBe('Enter your operator name.');
    expect(session.signIn).not.toHaveBeenCalled();
  });

  it('gives one message for every refusal and clears the password', async () => {
    session.signIn.mockRejectedValue(new HttpErrorResponse({ status: 401 }));

    typeInto(fixture, 'name', 'alice');
    typeInto(fixture, 'password', 'wrong password');
    clickButton(fixture, 'Sign in');
    await settle(fixture);
    expect(textOf(fixture, '[role=alert]')).toBe('Wrong name or password.');
    expect(element(fixture).querySelector<HTMLInputElement>('#password')?.value).toBe('');
  });

  it('shows the password form in a secure context', () => {
    expect(element(fixture).querySelector('#password')).not.toBeNull();
    expect(element(fixture).querySelector('[role=alert]')).toBeNull();
  });

  it('refuses to show the password form outside a secure context', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [provideRouter([]), { provide: Session, useValue: session }, { provide: SECURE_CONTEXT, useValue: false }],
    }).compileComponents();
    fixture = TestBed.createComponent(SignIn);
    document.body.appendChild(element(fixture));
    await fixture.whenStable();

    expect(textOf(fixture, '[role=alert]')).toBe('Operator sign-in needs HTTPS.');
    expect(document.activeElement).toBe(element(fixture).querySelector('[role=alert]'));
    expect(element(fixture).querySelector('form')).toBeNull();
    expect(element(fixture).querySelector('#password')).toBeNull();
  });
});
