import { describe, expect, it } from 'vitest';
import {
  formatNumberWithFormat,
  fractionDigitsInNumberFormat,
  groupsInNumberFormat,
} from './numberFormat';

/**
 * 2026-09-17, ငွေစာရင်း department: "ဒသမနောက် လေးလုံး မထည့်ပေးပါနှင့်".
 *
 * The fix is the '0.####' format, which needed the format reader to tell a REQUIRED
 * digit ('0') from an OPTIONAL one ('#') — it used to take the whole fraction length
 * as both the minimum and the maximum, so '0.####' would have printed 3000.0000 all
 * the same. These cases pin both halves: the new as-stored behaviour, and the ~150
 * other reports that must not move.
 */
describe('fractionDigitsInNumberFormat', () => {
  it('reads a required-digit format as a fixed width', () => {
    expect(fractionDigitsInNumberFormat('#,##0.0000')).toEqual({
      min: 4,
      max: 4,
    });
    expect(fractionDigitsInNumberFormat('0.0000')).toEqual({ min: 4, max: 4 });
    expect(fractionDigitsInNumberFormat('#,##0.00')).toEqual({ min: 2, max: 2 });
  });

  it('reads an optional-digit format as a range', () => {
    expect(fractionDigitsInNumberFormat('0.####')).toEqual({ min: 0, max: 4 });
    expect(fractionDigitsInNumberFormat('0.0##')).toEqual({ min: 1, max: 3 });
  });

  it('reads a format with no point as no decimals', () => {
    expect(fractionDigitsInNumberFormat('#,##0')).toEqual({ min: 0, max: 0 });
    expect(fractionDigitsInNumberFormat('0')).toEqual({ min: 0, max: 0 });
  });
});

describe('groupsInNumberFormat', () => {
  it('groups only when the format carries a comma', () => {
    expect(groupsInNumberFormat('#,##0.0000')).toBe(true);
    expect(groupsInNumberFormat('0.####')).toBe(false);
    expect(groupsInNumberFormat('#,##0')).toBe(true);
  });
});

describe('formatNumberWithFormat', () => {
  it("prints ငွေစာရင်း amounts as stored, with no padding", () => {
    expect(formatNumberWithFormat(3000, '0.####')).toBe('3000');
    expect(formatNumberWithFormat(3000.5, '0.####')).toBe('3000.5');
    expect(formatNumberWithFormat(12500.75, '0.####')).toBe('12500.75');
    expect(formatNumberWithFormat(0, '0.####')).toBe('0');
  });

  it('drops the float noise a SQL float column carries', () => {
    expect(formatNumberWithFormat(3000.0000000001, '0.####')).toBe('3000');
  });

  it('keeps no thousands separators in the Payment family', () => {
    expect(formatNumberWithFormat(1234567, '0.####')).toBe('1234567');
  });

  it('leaves the legacy RDLC formats exactly as they were', () => {
    expect(formatNumberWithFormat(3000, '#,##0.0000')).toBe('3,000.0000');
    expect(formatNumberWithFormat(2000.1, '#,##0.0000')).toBe('2,000.1000');
    expect(formatNumberWithFormat(3000, '#,##0')).toBe('3,000');
  });
});
