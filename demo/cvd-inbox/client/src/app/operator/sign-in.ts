import { HttpErrorResponse } from '@angular/common/http';
import { Component, InjectionToken, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { describeFailure } from '../api/problem';
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

  protected readonly insecure = !inject(SECURE_CONTEXT);

  protected readonly visibleError = visibleError;
  protected readonly failure = signal('');
  protected readonly model = signal({ name: '', password: '' });
  protected readonly signInForm = form(
    this.model,
    (path) => {
      required(path.name, { message: 'Enter your operator name.' });
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
            field().reset({ name, password: '' });
          }
        },
        onInvalid: (field) => field().errorSummary()[0]?.fieldTree().focusBoundControl(),
      },
    },
  );
}
