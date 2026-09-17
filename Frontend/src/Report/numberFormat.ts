/**
 * The one reading of a column's `numberFormat`, shared by the grid cell
 * (`GenericReportPage.toTableColumn`) and the grid's Total footer
 * (`BasicTable.formatColumnTotalValue`) so the two can never drift apart.
 *
 * The tokens are Excel's own, because the same string is posted to the backend in
 * the presentation spec and becomes the .xlsx cell format:
 *
 * - `0` after the point is a REQUIRED digit — it prints even when it is a zero.
 * - `#` after the point is an OPTIONAL digit — it prints only if the value has one.
 * - a `,` anywhere turns on thousands separators.
 *
 * So `'#,##0.0000'` (the legacy RDLC `N4`) always shows four places, `'#,##0'`
 * (`N0`) never shows a point, and `'0.####'` — the ငွေစာရင်း (Payment) reports,
 * 2026-09-17 — prints the value as stored: 3000 reads `3000`, 3000.5 reads
 * `3000.5`. The `####` cap also keeps SQL `float` noise (3000.0000000001) off the
 * screen.
 */
export interface FractionDigits {
  min: number;
  max: number;
}

export const fractionDigitsInNumberFormat = (
  numberFormat: string
): FractionDigits => {
  const fraction = numberFormat.split('.')[1] ?? '';
  return {
    min: (fraction.match(/0/g) ?? []).length,
    max: fraction.length,
  };
};

/**
 * Thousands separators come from the format string itself, exactly as they do in
 * Excel: '#,##0.00' groups, '0.00' does not. Every legacy RDLC format carries the
 * comma, so this is a no-op for them; the Payment reports ask for plain digits
 * (the accounting department re-keys the figures elsewhere).
 */
export const groupsInNumberFormat = (numberFormat: string) =>
  numberFormat.includes(',');

/** Renders a finite number the way `numberFormat` asks for it. */
export const formatNumberWithFormat = (
  value: number,
  numberFormat: string
): string => {
  const { min, max } = fractionDigitsInNumberFormat(numberFormat);
  return value.toLocaleString('en-US', {
    minimumFractionDigits: min,
    maximumFractionDigits: Math.max(min, max),
    useGrouping: groupsInNumberFormat(numberFormat),
  });
};
