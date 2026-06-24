namespace API.StoredProcedureToLinq;

/// <summary>
/// The single summary row shown by every PaThaKa registration "Summary Report"
/// (Whole Sale / Retail / Wine / Duty Free / BSA / Sale Center / Show Room / EV / EVCycle).
/// Mirrors the legacy RDLC summary grid's six count columns:
///   Number of Register / De-Register / Extension / Total Still Valid / Total Invalid / Total Number.
/// "Total Number" = ValidCount + InvalidCount (matches the old RDLC expression).
/// </summary>
public sealed class RegistrationSummaryRow
{
    public int NewCount { get; set; }
    public int CancelCount { get; set; }
    public int ExtensionCount { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }
    public int TotalNumber { get; set; }

    public static RegistrationSummaryRow Of(
        int newCount,
        int cancelCount,
        int extensionCount,
        int validCount,
        int invalidCount) => new()
        {
            NewCount = newCount,
            CancelCount = cancelCount,
            ExtensionCount = extensionCount,
            ValidCount = validCount,
            InvalidCount = invalidCount,
            TotalNumber = validCount + invalidCount,
        };
}
