import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, InjectionToken, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormField, FormRoot, form, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { describeFailure } from '../api/problem';
import { focusFirstError } from '../forms/focus-first-error';
import { trimmedLength } from '../forms/trimmed';
import { visibleError } from '../forms/visible-error';
import { Session } from './session';

/** Wraps window.isSecureContext so a spec can set it. */
export const SECURE_CONTEXT = new InjectionToken<boolean>('secure context', {
  providedIn: 'root',
  factory: () => window.isSecureContext,
});

@Component({
  selector: 'app-sign-in',
  imports: [FormField, FormRoot],
  templateUrl: './sign-in.html',
})
export class SignIn {
  private readonly session = inject(Session);
  private readonly router = inject(Router);
  private readonly insecureAlert = viewChild<ElementRef<HTMLElement>>('insecureAlert');

  protected readonly insecure = !inject(SECURE_CONTEXT);

  protected readonly visibleError = visibleError;
  protected readonly failure = signal('');
  protected readonly model = signal({ name: '', password: '' });
  protected readonly signInForm = form(
    this.model,
    (path) => {
      // The server trims the name too, so a name of spaces never reaches it.
      trimmedLength(path.name, { required: 'Enter your operator name.', max: [32, 'Operator names have at most 32 characters.'] });
      required(path.password, { message: 'Enter your password.' });
    },
    {
      submission: {
        action: async (field) => {
          this.failure.set('');
          const { name, password } = field().value();
          try {
            await this.session.ensureLoaded();
            await this.session.signIn(name, password);
            await this.router.navigateByUrl('/operator');
          } catch (error) {
            this.failure.set(
              error instanceof HttpErrorResponse && error.status === 401 ? 'Wrong name or password.' : describeFailure(error),
            );
          } finally {
            // The password leaves the form model once it has been sent, whatever the answer.
            field().reset({ name, password: '' });
          }
        },
        onInvalid: focusFirstError,
      },
    },
  );

  constructor() {
    // Nothing on this page works here: move the reader straight to the reason.
    if (this.insecure) afterNextRender(() => this.insecureAlert()?.nativeElement.focus());
  }
}
