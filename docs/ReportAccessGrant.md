# Report-only view-all grant

`ReportAccess:ViewAllReportUserIds` is an optional backend configuration array of
TradeNet `dbo.Users.Id` values. Only **active, non-deleted users whose current
`UserType` is `Report`** receive the grant. An absent or invalid entry grants
nothing. The list number in the Users screen is not the database ID.

For this assignment, the verified TradeNet user ID for `knyeinthu_Rpt` is
`1503`. Add this to the **server's** backend configuration when activating the
feature:

```json
{
  "ReportAccess": {
    "ViewAllReportUserIds": [1503]
  }
}
```

The equivalent environment setting is
`ReportAccess__ViewAllReportUserIds__0=1503`. The deployment script preserves
the server's `appsettings*.json`, so publishing code alone does **not** activate
this grant. Do not put account passwords in configuration or in this journal.

The grant covers all report menus, report API requests, direct report routes,
and the user's **own** Excel exports for any report. It does not set `IsAdmin`,
change the JWT role or database `UserType`, allow Data Import or Activity Log,
or allow access to another user's exports. Existing `UserDetail` restrictions
continue to apply to everyone not configured here. No database migration or
stored procedure change is required.

To verify after deployment, sign in as the report user and check reports from
unassigned families, Excel creation/download, and 403 for Data Import and
Activity Log. Also sign in as another Report user to confirm their assigned
family limits remain. To revoke, remove ID `1503` from the server setting and
restart the backend; the next permission load will revert to `UserDetail` scope.
