import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormField, FormRoot, form } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { ReportDetail, ReportStatus, statusLabels } from '../api/models';
import { describeFailure, fieldProblems } from '../api/problem';
import { serverFieldErrors } from '../forms/server-errors';
import { trimmedLength } from '../forms/trimmed';
import { visibleError } from '../forms/visible-error';

@Component({
  selector: 'app-report-view',
  imports: [DatePipe, FormField, FormRoot, RouterLink],
  templateUrl: './report-view.html',
})
export class ReportView {
  private readonly api = inject(InboxApi);
  private readonly router = inject(Router);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));
  private readonly actions = viewChild<ElementRef<HTMLElement>>('actions');

  protected readonly labels = statusLabels;
  protected readonly visibleError = visibleError;
  protected readonly report = signal<ReportDetail | null>(null);
  protected readonly failure = signal('');
  protected readonly statusError = signal('');
  protected readonly confirmingDelete = signal(false);
  protected readonly noteModel = signal({ text: '' });
  protected readonly noteForm = form(
    this.noteModel,
    (path) => {
      trimmedLength(path.text, { required: 'Write the note first.', max: [4000, 'Use at most 4,000 characters.'] });
    },
    {
      submission: {
        action: async (field) => {
          this.failure.set('');
          try {
            const note = await this.api.addNote(this.id, field().value().text);
            this.report.update((report) => (report ? { ...report, notes: [...report.notes, note] } : report));
            this.noteModel.set({ text: '' });
            this.noteForm().reset();
          } catch (error) {
            const fieldErrors = serverFieldErrors(error, { text: field.text });
            if (fieldErrors) return fieldErrors;
            this.failure.set(describeFailure(error));
          }
          return undefined;
        },
        onInvalid: (field) => field().errorSummary()[0]?.fieldTree().focusBoundControl(),
      },
    },
  );

  constructor() {
    void this.load();
  }

  protected async move(status: ReportStatus): Promise<void> {
    this.failure.set('');
    this.statusError.set('');
    try {
      this.report.set(await this.api.changeStatus(this.id, status));
    } catch (error) {
      const problem = fieldProblems(error)?.['status'];
      if (problem) {
        // The status buttons are not a form field: show the message next to them and focus the first one.
        this.statusError.set(problem);
        this.actions()?.nativeElement.querySelector('button')?.focus();
      } else {
        this.failure.set(describeFailure(error));
      }
    }
  }

  protected async remove(): Promise<void> {
    try {
      await this.api.deleteReport(this.id);
      await this.router.navigateByUrl('/operator');
    } catch (error) {
      this.failure.set(describeFailure(error));
      this.confirmingDelete.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.report.set(await this.api.report(this.id));
    } catch (error) {
      this.failure.set(
        error instanceof HttpErrorResponse && error.status === 404
          ? 'This report does not exist. It may have been deleted.'
          : describeFailure(error),
      );
    }
  }
}
