import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  Disclosure,
  NewReport,
  Receipt,
  ReportDetail,
  ReportNote,
  ReportStatus,
  ReportStatusView,
  ReportSummary,
  SessionInfo,
} from './models';

/** The server's API. HttpClient adds the X-XSRF-TOKEN header to POST and DELETE requests by itself. */
@Injectable({ providedIn: 'root' })
export class InboxApi {
  private readonly http = inject(HttpClient);

  submitReport(report: NewReport): Promise<Receipt> {
    return firstValueFrom(this.http.post<Receipt>('/api/reports', report));
  }

  lookUpStatus(receipt: string): Promise<ReportStatusView> {
    return firstValueFrom(this.http.post<ReportStatusView>('/api/reports/status', { receipt }));
  }

  disclosure(): Promise<Disclosure> {
    return firstValueFrom(this.http.get<Disclosure>('/api/disclosure'));
  }

  session(): Promise<SessionInfo> {
    return firstValueFrom(this.http.get<SessionInfo>('/api/auth/session'));
  }

  signIn(name: string, password: string): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/auth/sign-in', { name, password }));
  }

  signOut(): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/auth/sign-out', {}));
  }

  reports(status?: ReportStatus): Promise<ReportSummary[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return firstValueFrom(this.http.get<ReportSummary[]>('/api/inbox', { params }));
  }

  report(id: number): Promise<ReportDetail> {
    return firstValueFrom(this.http.get<ReportDetail>(`/api/inbox/${id}`));
  }

  changeStatus(id: number, status: ReportStatus): Promise<ReportDetail> {
    return firstValueFrom(this.http.post<ReportDetail>(`/api/inbox/${id}/status`, { status }));
  }

  addNote(id: number, text: string): Promise<ReportNote> {
    return firstValueFrom(this.http.post<ReportNote>(`/api/inbox/${id}/notes`, { text }));
  }

  deleteReport(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/inbox/${id}`));
  }
}
