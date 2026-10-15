export type ReportStatus = 'new' | 'triage' | 'accepted' | 'rejected' | 'fixed' | 'disclosed';

export const statusLabels: Record<ReportStatus, string> = {
  new: 'New',
  triage: 'Triage',
  accepted: 'Accepted',
  rejected: 'Rejected',
  fixed: 'Fixed',
  disclosed: 'Disclosed',
};

export interface NewReport {
  product: string;
  version: string;
  description: string;
  contact: string;
  website: string;
}

export interface Receipt {
  receipt: string;
}

export interface ReportStatusView {
  status: ReportStatus;
  receivedAt: string;
  updatedAt: string;
}

export interface Disclosure {
  reportUrl: string;
  contact: string[];
  expires: string;
  preferredLanguages: string;
  acknowledgeWithinDays: number;
  disclosureAfterDays: number;
  contactRetentionDays: number;
}

export interface SessionInfo {
  operator: string | null;
}

export interface ReportSummary {
  id: number;
  product: string;
  version: string;
  status: ReportStatus;
  receivedAt: string;
  updatedAt: string;
}

export interface ReportNote {
  id: number;
  author: string;
  text: string;
  createdAt: string;
}

export interface ReportDetail extends ReportSummary {
  description: string;
  contact: string | null;
  contactErasedAt: string | null;
  closedAt: string | null;
  nextStatuses: ReportStatus[];
  notes: ReportNote[];
}
