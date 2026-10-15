# Data handling

Annex I, Part I, points 2(g) and 2(m) of the Cyber Resilience Act: data minimisation and secure removal of data.

## What CVD Inbox stores

| Data | Where | Why | Kept until |
|---|---|---|---|
| Product, version and description of a report | `Reports` table | The report itself | An operator deletes the report |
| Researcher's contact, optional | `Reports.Contact` | Follow-up questions | `Inbox__ContactRetentionDays` (30) days after the report is closed |
| SHA-256 of the receipt code | `Reports.ReceiptHash` | Status lookups | The report is deleted |
| Status and dates | `Reports` table | Triage and the status page | The report is deleted |
| Internal notes and their author | `Notes` table | Triage | The report is deleted |
| Operator name, password hash, failed sign-in count, lock time | `Operators` table | Sign-in | The database is removed |
| Data protection keys | `keys/` folder | Protect the session and request token cookies | You delete the data folder |

What it does not store:

- the receipt code itself, only its hash, so a copy of the database does not let anyone follow a report;
- the researcher's network address, which is not logged with a report;
- analytics or tracking of any kind.

The log, on standard output, records the events listed in [security-events.md](security-events.md). It never contains report text, contacts, receipt codes, notes or passwords. A name typed in the sign-in form is logged only when it belongs to an existing operator.

## Retention

A report closes when it moves to rejected or disclosed. From then on its contact is kept for `Inbox__ContactRetentionDays` days and then erased; the report shows the date of the erasure. Reopening a rejected report before that stops the clock. A background job checks at start and then every `Inbox__RetentionSweepInterval`, one hour by default.

## Deletion

An operator can delete a report for good. The report and its notes go in one statement.

SQLite keeps deleted content in free pages and in the write-ahead log unless told otherwise. CVD Inbox:

- turns on `PRAGMA secure_delete` on every connection, so freed space is overwritten with zeros;
- truncates the write-ahead log after each deletion (`PRAGMA wal_checkpoint(TRUNCATE)`), and again at the end of every retention sweep, also when the sweep erased nothing.

A reader that is still open can keep the checkpoint from finishing. The app retries a few times, then gives up and logs event 1204 without any report content. The deletion has committed by then, and the next sweep truncates the log again. The truncate belongs to the sweep, so the contacts it erases leave the log within the same sweep.

The tests read the raw database files after a deletion and after an erasure and check that the text is gone: `Deleting_a_report_leaves_no_trace_in_the_database_files` and `Erased_contacts_leave_no_bytes_in_the_database_files`.

## Limits

- Backups, volume snapshots and copies made before a deletion keep the old data. Apply the same retention to them.
- Disks and SSDs can keep old blocks after a file is overwritten. Use an encrypted volume.
- The app does not encrypt the database or the keys. Protect the data volume.
- There is no export. To move the inbox to another host, stop it and copy the data folder over an encrypted channel.

## Removing everything

Delete the data folder or the volume; see [operations.md](operations.md#remove-everything).
