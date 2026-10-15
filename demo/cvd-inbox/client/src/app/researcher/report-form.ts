import { Component, ElementRef, Injector, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormField, FormRoot, form, maxLength, minLength, required } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { describeFailure } from '../api/problem';
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
      required(path.product, { message: 'Enter the name of the product.' });
      maxLength(path.product, 100, { message: 'Use at most 100 characters.' });
      required(path.version, { message: 'Enter the version you tested.' });
      maxLength(path.version, 50, { message: 'Use at most 50 characters.' });
      required(path.description, { message: 'Describe the vulnerability.' });
      minLength(path.description, 20, { message: 'Write at least 20 characters.' });
      maxLength(path.description, 20000, { message: 'Use at most 20,000 characters.' });
      maxLength(path.contact, 200, { message: 'Use at most 200 characters.' });
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
            this.failure.set(describeFailure(error));
          }
        },
        onInvalid: (field) => field().errorSummary()[0]?.fieldTree().focusBoundControl(),
      },
    },
  );
}
