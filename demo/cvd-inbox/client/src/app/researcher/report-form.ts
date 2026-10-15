import { Component, ElementRef, Injector, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormField, FormRoot, form } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { describeFailure } from '../api/problem';
import { focusFirstError } from '../forms/focus-first-error';
import { serverFieldErrors } from '../forms/server-errors';
import { trimmedLength } from '../forms/trimmed';
import { visibleError } from '../forms/visible-error';

@Component({
  selector: 'app-report-form',
  imports: [FormField, FormRoot, RouterLink],
  templateUrl: './report-form.html',
})
export class ReportForm {
  private readonly api = inject(InboxApi);
  private readonly injector = inject(Injector);
  private readonly receiptHeading = viewChild<ElementRef<HTMLElement>>('receiptHeading');

  protected readonly visibleError = visibleError;
  protected readonly receipt = signal('');
  protected readonly failure = signal('');
  protected readonly model = signal({ product: '', version: '', description: '', contact: '', website: '' });
  protected readonly reportForm = form(
    this.model,
    (path) => {
      trimmedLength(path.product, { required: 'Enter the name of the product.', max: [100, 'Use at most 100 characters.'] });
      trimmedLength(path.version, { required: 'Enter the version you tested.', max: [50, 'Use at most 50 characters.'] });
      trimmedLength(path.description, {
        required: 'Describe the vulnerability.',
        min: [20, 'Write at least 20 characters.'],
        max: [20000, 'Use at most 20,000 characters.'],
      });
      trimmedLength(path.contact, { max: [200, 'Use at most 200 characters.'] });
    },
    {
      submission: {
        action: async (field) => {
          this.failure.set('');
          try {
            const { receipt } = await this.api.submitReport(field().value());
            this.receipt.set(receipt);
            afterNextRender(() => this.receiptHeading()?.nativeElement.focus(), { injector: this.injector });
          } catch (error) {
            const fieldErrors = serverFieldErrors(error, {
              product: field.product,
              version: field.version,
              description: field.description,
              contact: field.contact,
            });
            if (fieldErrors) return fieldErrors;
            this.failure.set(describeFailure(error));
          }
          return undefined;
        },
        onInvalid: focusFirstError,
      },
    },
  );
}
