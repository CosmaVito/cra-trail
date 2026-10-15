import { Routes } from '@angular/router';
import { InboxList } from './operator/inbox-list';
import { operatorGuard } from './operator/operator-guard';
import { ReportView } from './operator/report-view';
import { SignIn } from './operator/sign-in';
import { ReportForm } from './researcher/report-form';
import { SecurityPolicy } from './researcher/security-policy';
import { StatusLookup } from './researcher/status-lookup';

export const routes: Routes = [
  { path: '', component: ReportForm, title: 'Report a vulnerability · CVD Inbox' },
  { path: 'status', component: StatusLookup, title: 'Report status · CVD Inbox' },
  { path: 'security-policy', component: SecurityPolicy, title: 'Security policy · CVD Inbox' },
  { path: 'operator/sign-in', component: SignIn, title: 'Operator sign-in · CVD Inbox' },
  { path: 'operator', component: InboxList, canActivate: [operatorGuard], title: 'Inbox · CVD Inbox' },
  { path: 'operator/reports/:id', component: ReportView, canActivate: [operatorGuard], title: 'Report · CVD Inbox' },
  { path: '**', redirectTo: '' },
];
