using System.Text;

namespace API.Service.Reports;

/// <summary>
/// The Tradenet 2.0 admin's <c>CommonRepository.GetAddress</c>, reproduced character for
/// character. The old Detail reports (e.g. <c>BorderImportPermitDetailReport.rdlc</c>'s
/// "Company Address" cell) print this string, not the six address columns, and its quirks are
/// part of what the customer sees: a trailing ", " when the country is blank, and
/// "State," with no space when there is a state but no postal code. The grid used to join the
/// six columns with ", " in a different order; that was visibly different from the old report.
/// </summary>
public static class LegacyCompanyAddress
{
    public static string Compose(
        string? unitLevel,
        string? streetNumberStreetName,
        string? quarterCityTownship,
        string? state,
        string? country,
        string? postalCode)
    {
        // Legacy: `row["X"].ToString()` turns a NULL into "" before the checks below, so
        // null and empty are the same thing here. Whitespace-only values are kept, as there.
        var address = new StringBuilder();

        if (!string.IsNullOrEmpty(unitLevel))
        {
            address.Append(unitLevel).Append(", ");
        }

        if (!string.IsNullOrEmpty(streetNumberStreetName))
        {
            address.Append(streetNumberStreetName).Append(", ");
        }

        if (!string.IsNullOrEmpty(quarterCityTownship))
        {
            address.Append(quarterCityTownship).Append(", ");
        }

        if (!string.IsNullOrEmpty(state))
        {
            if (!string.IsNullOrEmpty(postalCode))
            {
                address.Append(state).Append(' ').Append(postalCode).Append(", ");
            }
            else
            {
                // Legacy has no space after this comma. Deliberate: same bytes as the old report.
                address.Append(state).Append(',');
            }
        }

        if (!string.IsNullOrEmpty(country))
        {
            address.Append(country);
        }

        return address.ToString();
    }
}
