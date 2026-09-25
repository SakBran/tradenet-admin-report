import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { buildReportHeaderLines, formatDateCell } from '../reportPresentation';
import { formatNumberWithFormat } from '../numberFormat';

/**
 * The eleven Registration-By-Voucher reports against the old Tradenet 2.0 screens.
 *
 * 2026-09-25, BSA: "excel ကော UI မှာကော ခေါင်းစဉ်(New/Amend) ထုတ်ရင် မပါလာလို့ပါ ...
 * ဒသမနောက်က သုညလေးလုံး ဖြုတ်ပေးပါရန်". The owner then had the title fixed on every
 * sibling too, plus the dd/MM/yyyy dates — on these reports only.
 *
 * - Title: every old action sets header1 = "<Family> " + ApplyType + " List (From) To (To)"
 *   (ReportsController.cs on origin/master :669 PaThaKa, :1088 Whole Sale, :1294 Retail,
 *   :1500 Whole Sale & Retail, :1702 Alcoholic Beverages Importation, :1904 Duty Free
 *   Shop, :2956 Business Service Agency); the Sale Center / Show Room family prints the
 *   selected model.RegistrationType (:2125, :2342, :2549, :2755). The same header lines
 *   feed the grid and the Excel spec, so pinning them covers both.
 * - Dates: the RDLCs print sDate / sVoucherDate, already .ToString("dd/MM/yyyy").
 * - Filters: every old form asks Apply Type before Payment Type; the RDLC's first
 *   header is "No.".
 * - Decimals: BSA only prints Total Amount as stored — the siblings keep four places.
 */
const TITLED: Array<[string, string]> = [
  ['WholeSaleRegistrationByVoucher', 'Whole Sale'],
  ['RetailRegistrationByVoucher', 'Retail'],
  ['WholeSaleAndRetailRegistrationByVoucher', 'Whole Sale & Retail'],
  ['AlcoholicBeveragesImportationRegistrationByVoucher', 'Alcoholic Beverages Importation'],
  ['DutyFreeShopRegistrationByVoucher', 'Duty Free Shop'],
  ['BusinessServiceAgencyRegistrationByVoucher', 'Business Service Agency'],
  ['SaleCenterRegistrationByVoucher', 'Sale Center'],
  ['ShowRoomRegistrationByVoucher', 'Show Room'],
  ['EVShowRoomRegistrationByVoucher', 'Show Room for Electric Vehicles'],
  ['EVCycleShowRoomRegistrationByVoucher', 'Show Room for Electric Cycles'],
];

// PaThaKa had its title already (and its own filter history), so it is only in the
// date rule.
const ALL_VOUCHERS = ['RegistrationByVoucher', ...TITLED.map(([key]) => key)];

const WITH_FORM_TYPE = new Set([
  'SaleCenterRegistrationByVoucher',
  'ShowRoomRegistrationByVoucher',
  'EVShowRoomRegistrationByVoucher',
  'EVCycleShowRoomRegistrationByVoucher',
]);

const applied = (ApplyType: string, FormType = '') => ({
  FromDate: '2026-01-01T00:00:00',
  ToDate: '2026-01-31T23:59:59',
  ApplyType,
  PaymentType: '',
  FormType,
});

describe('Registration-By-Voucher reports — legacy parity', () => {
  describe.each(TITLED)('%s', (key, family) => {
    const cfg = reportConfigs[key];

    it.each(['New', 'Amend'])('the title line names the Apply Type (%s)', (applyType) => {
      expect(buildReportHeaderLines(cfg, applied(applyType))).toEqual([
        'Ministry of Commerce',
        'Directorate of Trade',
        `${family} ${applyType} List (01/01/2026) To (31/01/2026)`,
      ]);
    });

    it('asks Apply Type before Payment Type, and heads the row number "No."', () => {
      expect(cfg.filters.map((filter) => filter.name)).toEqual(
        WITH_FORM_TYPE.has(key)
          ? ['dateRange', 'FormType', 'ApplyType', 'PaymentType']
          : ['dateRange', 'ApplyType', 'PaymentType']
      );
      expect(cfg.rowNumberTitle).toBe('No.');
    });
  });

  it('prints the selected Form Type in place of the family, as the old RegistrationType did', () => {
    expect(
      buildReportHeaderLines(
        reportConfigs.SaleCenterRegistrationByVoucher,
        applied('Amend', 'Sale Center for Commercial Vehicles')
      ).at(-1)
    ).toBe('Sale Center for Commercial Vehicles Amend List (01/01/2026) To (31/01/2026)');
    expect(
      buildReportHeaderLines(
        reportConfigs.ShowRoomRegistrationByVoucher,
        applied('New', 'Show Room for Machinery and Mechanical')
      ).at(-1)
    ).toBe('Show Room for Machinery and Mechanical New List (01/01/2026) To (31/01/2026)');
  });

  it.each(ALL_VOUCHERS)('%s prints its dates dd/MM/yyyy, grid and picker', (key) => {
    const cfg = reportConfigs[key];
    const dateColumns = cfg.columns.filter((column) => column.dataType === 'date');

    expect(dateColumns.map((column) => column.key)).toEqual(['Date', 'VoucherDate']);
    for (const column of dateColumns) {
      expect(column.dateFormat, column.key).toBe('DD/MM/YYYY');
      expect(formatDateCell('2026-01-06T00:00:00', column.dateFormat!)).toBe('06/01/2026');
    }
    expect(cfg.filters.find((filter) => filter.type === 'dateRange')?.displayFormat).toBe(
      'DD/MM/YYYY'
    );
  });

  it('BSA prints Total Amount as stored; the siblings keep four places', () => {
    const totalAmount = (key: string) =>
      reportConfigs[key].columns.find((column) => column.key === 'TotalAmount')?.numberFormat;

    expect(totalAmount('BusinessServiceAgencyRegistrationByVoucher')).toBe('0.####');
    expect(formatNumberWithFormat(260000, '0.####')).toBe('260000');
    expect(formatNumberWithFormat(2500.5, '0.####')).toBe('2500.5');

    for (const key of ALL_VOUCHERS.filter(
      (candidate) => candidate !== 'BusinessServiceAgencyRegistrationByVoucher'
    )) {
      expect(totalAmount(key), key).toBe('#,##0.0000');
    }
  });

  it('keeps the BSA column set', () => {
    expect(
      reportConfigs.BusinessServiceAgencyRegistrationByVoucher.columns.map(
        (column) => column.title
      )
    ).toEqual([
      'Date',
      'Company Registration No',
      'Company Name',
      'Company Address',
      'BSA No',
      'Agent of Authorize Company',
      'Total Amount',
      'Payment Type',
      'Voucher No',
      'Voucher Date',
    ]);
  });

  // The date rule is for these eleven only (the owner's scope). Their Summary /
  // Detail siblings share the column and filter factories, so a leak shows up here.
  it.each([
    'BusinessServiceAgencyDetailReport',
    'SaleCenterDetailReport',
    'WholeSaleDetailReport',
    'ExportLicenceAmendmentReport',
  ])('%s keeps the default YYYY-MM-DD dates', (key) => {
    const cfg = reportConfigs[key];
    expect(cfg, key).toBeDefined();
    expect(cfg.columns.filter((column) => column.dateFormat)).toEqual([]);
    expect(cfg.filters.filter((filter) => filter.displayFormat)).toEqual([]);
  });
});
