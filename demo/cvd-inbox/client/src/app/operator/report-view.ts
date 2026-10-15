import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormField, FormRoot, form } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { ReportDetail, ReportStatus, statusLabels } from '../api/models';
import { describeFailure } from '../api/problem';
import { focusFirstError } from '../forms/focus-first-error';
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
  private readonly injector = inject(Injector);
  private readonly statusHeading = viewChild<ElementRef<HTMLElement>>('statusHeading');
  private readonly deleteButton = viewChild<ElementRef<HTMLElement>>('deleteButton');
  private readonly confirmButton = viewChild<ElementRef<HTMLElement>>('confirmButton');

  protected readonly labels = statusLabels;
  protected readonly visibleError = visibleError;
  protected readonly report = signal<ReportDetail | null>(null);
  protected readonly failure = signal('');
  protected readonly announcement = signal('');
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
          this.announcement.set('');
          try {
            const note = await this.api.addNote(this.id, field().value().text);
            this.report.update((report) => (report ? { ...report, notes: [...report.notes, note] } : report));
            this.noteModel.set({ text: '' });
            this.noteForm().reset();
            this.announcement.set('Note added');
          } catch (error) {
            const fieldErrors = serverFieldErrors(error, { text: field.text });
            if (fieldErrors) return fieldErrors;
            this.failure.set(describeFailure(error));
          }
          return undefined;
        },
        onInvalid: focusFirstError,
      },
    },
  );

  constructor() {
    void this.load();
  }

  protected async move(status: ReportStatus): Promise<void> {
    this.failure.set('');
    this.announcement.set('');
    try {
      this.report.set(await this.api.changeStatus(this.id, status));
      this.announcement.set(`Status changed to ${this.labels[status]}`);
      // The button that was clicked is gone, so focus would fall to the page.
      this.focusAfterRender(this.statusHeading);
    } catch (error) {
      this.failure.set(describeFailure(error));
      // 409: the status changed under this operator, so the buttons on the page are stale.
      if (error instanceof HttpErrorResponse && error.status === 409) await this.load();
    }
  }

  protected askToDelete(): void {
    this.confirmingDelete.set(true);
    this.focusAfterRender(this.confirmButton);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(false);
    this.focusAfterRender(this.deleteButton);
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

  private focusAfterRender(target: () => ElementRef<HTMLElement> | undefined): void {
    afterNextRender(() => target()?.nativeElement.focus(), { injector: this.injector });
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
