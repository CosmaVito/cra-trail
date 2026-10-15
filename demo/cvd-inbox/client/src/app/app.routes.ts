import { Routes } from '@angular/router';
import { ReportForm } from './researcher/report-form';
import { SecurityPolicy } from './researcher/security-policy';
import { StatusLookup } from './researcher/status-lookup';

export const routes: Routes = [
  { path: '', component: ReportForm, title: 'Report a vulnerability · CVD Inbox' },
  { path: 'status', component: StatusLookup, title: 'Report status · CVD Inbox' },
  { path: 'security-policy', component: SecurityPolicy, title: 'Security policy · CVD Inbox' },
  { path: '**', redirectTo: '' },
];
