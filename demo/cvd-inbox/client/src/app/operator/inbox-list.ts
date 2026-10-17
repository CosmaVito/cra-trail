import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
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
  protected readonly loading = signal(true);
  protected readonly announcement = computed(() => {
    const list = this.reports();
    if (this.loading()) return 'Loading reports';
    if (!list) return '';
    return list.length === 1 ? '1 report' : `${list.length} reports`;
  });
  private latest = 0;

  constructor() {
    void this.load();
  }

  protected async changeFilter(value: string): Promise<void> {
    this.filter.set(value as ReportStatus | '');
    await this.load();
  }

  private async load(): Promise<void> {
    // An answer to an earlier filter can come back after the latest one; only the latest counts.
    const request = ++this.latest;
    this.failure.set('');
    this.loading.set(true);
    try {
      const reports = await this.api.reports(this.filter() || undefined);
      if (request === this.latest) this.reports.set(reports);
    } catch (error) {
      if (request === this.latest) this.failure.set(describeFailure(error));
    } finally {
      if (request === this.latest) this.loading.set(false);
    }
  }
}
