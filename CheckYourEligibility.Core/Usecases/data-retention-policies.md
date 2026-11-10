To ensure the ECE platform can continue to function with an ever-growing dataset, appropriate retention periods need to be implemented across a number of significant data types.

# Required features

Retention policies need to support arbitrary SQL criteria, including conditions in addition to a time cutoff. For example:

- `EligibilityCheckHashes` where `[Type] != 'WorkingFamilies'` and `[Timestamp]` is older than 30 days
- `EligibilityCheckReports` and `EligibilityCheckReportItems` where the report `[IsDeleted] = 1` and `[Timestamp]` is older than 30 days

These examples illustrate the kinds of criteria policies may use; the policy SQL is configured per data type. Initially, policies will be configured manually in SQL Server Management Studio (SSMS).

The mechanism needs to handle relationships between records, including:

- Configurable reference updates to detach records before deletion (for example, hash references in checks)
- Configurable cascades to delete child records (for example, report items)

Each policy must support configuring one or more reference updates and/or cascade actions.

Policies need independent execution frequencies, such as:

- Daily at a specified time
- Weekly on a specified day and time

A shared timer-triggered function will check for due policies and start their enforcement processes.

Execution must support dry runs, deletion previews/counts, batched deletion, retries, and prevention of overlapping runs. Each run must produce a summary in Application Insights. Execution details must include status, start/end time, affected-row counts, errors, and policy version. Errors and warnings must also be logged to Application Insights. The system will handle retention of execution details.

# Agreed implementation

The retention policy system will use a new table to store policy configuration and execution state, including status and last-run time. Models and tables will be created in the active branch of `https://github.com/DFE-Digital/eligibility-checking-engine` (`elig-3360-api-logic-refactor`) and published as a NuGet package for consumption by the function app project at `https://github.com/DFE-Digital/eligibility-checking-engine-functions`.

The Core/NuGet package will provide the models and policy storage. The function app will use a shared timer to identify due policies and start Durable Functions to enforce them. Each policy will retain its own frequency and configured SQL criteria.

# Proposed rules

The SQL criteria for each policy determine the exact affected tables and records. The data types below are policy categories; the mappings and selection criteria are configured per policy.

| Data type | Table | Retention period | Proposed action |
|-----------|------------------|-----------------|
| CAPI audit logs | CapiAudit | 90 days | Permanently delete |
| Working Families dual-running logs | WorkingFamiliesDualRunningCheck | 90 days | Permanently delete |
| Reports | EligibilityCheckReport | 120 days | Permanently delete |
| Checks | EligibilityCheck | 180 days | Permanently delete |
| Bulk Checks | BulkChecks | 180 days | Permanently delete, aligned to Checks |
| Applications | Applications | 7 years (confirmed by the census team for funding/audit purposes) | Permanently delete, **subject to final governance/audit confirmation** |
| Unlinked Users | Users | TBC (90 days proposed) | Permanently delete |
