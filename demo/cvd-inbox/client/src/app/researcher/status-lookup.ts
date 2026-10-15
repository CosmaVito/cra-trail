import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, pattern, required } from '@angular/forms/signals';
import { InboxApi } from '../api/inbox-api';
import { ReportStatusView, statusLabels } from '../api/models';
import { describeFailure } from '../api/problem';
import { focusFirstError } from '../forms/focus-first-error';
import { serverFieldErrors } from '../forms/server-errors';
import { visibleError } from '../forms/visible-error';

@Component({
  selector: 'app-status-lookup',
  imports: [DatePipe, FormField, FormRoot],
  templateUrl: './status-lookup.html',
})
export class StatusLookup {
  private readonly api = inject(InboxApi);

  protected readonly labels = statusLabels;
  protected readonly visibleError = visibleError;
  protected readonly result = signal<ReportStatusView | null>(null);
  protected readonly failure = signal('');
  protected readonly model = signal({ receipt: '' });
  protected readonly lookupForm = form(
    this.model,
    (path) => {
      required(path.receipt, { message: 'Enter your receipt code.' });
      pattern(path.receipt, /^[\s-]*(?:[0-9a-fA-F][\s-]*){32}$/, {
        message: 'Receipt codes have 32 letters and digits, from 0-9 and a-f.',
      });
    },
    {
      submission: {
        action: async (field) => {
          this.failure.set('');
          this.result.set(null);
          try {
            this.result.set(await this.api.lookUpStatus(field().value().receipt));
          } catch (error) {
            const fieldErrors = serverFieldErrors(error, { receipt: field.receipt });
            if (fieldErrors) return fieldErrors;
            this.failure.set(
              error instanceof HttpErrorResponse && error.status === 404
                ? 'No report matches this receipt code. Check it and try again.'
                : describeFailure(error),
            );
          }
          return undefined;
        },
        onInvalid: focusFirstError,
      },
    },
  );
}
