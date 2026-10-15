import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { ReportStatus, ReportSummary, statusLabels } from '../api/models';
import { describeFailure } from '../api/problem';

@Component({
  selector: 'app-inbox-list',
  imports: [DatePipe, RouterLink],
  templateUrl: './inbox-list.html',
})
export class InboxList {
  private readonly api = inject(InboxApi);

  protected readonly labels = statusLabels;
  protected readonly statuses = Object.keys(statusLabels) as ReportStatus[];
  protected readonly filter = signal<ReportStatus | ''>('');
  protected readonly reports = signal<ReportSummary[] | null>(null);
  protected readonly failure = signal('');

  constructor() {
    void this.load();
  }

  protected async changeFilter(value: string): Promise<void> {
    this.filter.set(value as ReportStatus | '');
    await this.load();
  }

  private async load(): Promise<void> {
    this.failure.set('');
    try {
      this.reports.set(await this.api.reports(this.filter() || undefined));
    } catch (error) {
      this.failure.set(describeFailure(error));
    }
  }
}
