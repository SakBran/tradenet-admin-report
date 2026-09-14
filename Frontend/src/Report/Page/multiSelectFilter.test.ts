import { describe, expect, it } from 'vitest';
import dayjs from 'dayjs';
import { getInitialFilterValue, normalizeFilters } from './GenericReportPage';
import { ReportFilterConfig } from '../config/reportTypes';

// The `multiSelect` filter type and the `defaultDateRangeMonths: 0` default were both added
// for Advance Search, whose legacy screen posted its three `@Html.ListBoxFor` boxes as one
// comma-joined string and opened both date boxes on today.

const countryOfOrigin: ReportFilterConfig = {
  name: 'CountryOfOrigin',
  label: 'Country of Origin',
  type: 'multiSelect',
  lookupName: 'countries',
};

const modeOfTransport: ReportFilterConfig = {
  name: 'ModeOfTransport',
  label: 'Mode of Transport',
  type: 'multiSelect',
  options: [
    { label: 'Sea', value: 'Sea' },
    { label: 'Road', value: 'Road' },
    { label: 'Air', value: 'Air' },
  ],
};

describe('multiSelect filters', () => {
  it('starts empty, which is what "all" means to these filters', () => {
    expect(getInitialFilterValue(countryOfOrigin)).toEqual([]);
  });

  it('posts the selection as one comma-joined string', () => {
    expect(
      normalizeFilters([countryOfOrigin], { CountryOfOrigin: [5, 12] })
    ).toEqual({ CountryOfOrigin: '5,12' });
  });

  it('posts a single selection with no separator', () => {
    expect(
      normalizeFilters([modeOfTransport], { ModeOfTransport: ['Sea'] })
    ).toEqual({ ModeOfTransport: 'Sea' });
  });

  it('posts "" when nothing is selected, not "0" and not undefined', () => {
    expect(normalizeFilters([countryOfOrigin], { CountryOfOrigin: [] })).toEqual(
      { CountryOfOrigin: '' }
    );
    expect(
      normalizeFilters([countryOfOrigin], { CountryOfOrigin: undefined })
    ).toEqual({ CountryOfOrigin: '' });
  });

  it('keeps the option text for Mode of Transport, not the S/R/A codes', () => {
    expect(
      normalizeFilters([modeOfTransport], {
        ModeOfTransport: ['Sea', 'Road', 'Air'],
      })
    ).toEqual({ ModeOfTransport: 'Sea,Road,Air' });
  });
});

describe('defaultDateRangeMonths: 0', () => {
  const todayToToday: ReportFilterConfig = {
    name: 'dateRange',
    label: 'From Date / To Date',
    type: 'dateRange',
    fromName: 'FromDate',
    toName: 'ToDate',
    required: true,
    defaultDateRangeMonths: 0,
  };

  it('opens both boxes on today', () => {
    const [from, to] = getInitialFilterValue(todayToToday) as [
      dayjs.Dayjs,
      dayjs.Dayjs,
    ];
    const today = dayjs().format('YYYY-MM-DD');

    expect(from.format('YYYY-MM-DD')).toBe(today);
    expect(to.format('YYYY-MM-DD')).toBe(today);
  });

  it('still opens on the start of the month when unset', () => {
    const [from] = getInitialFilterValue({
      ...todayToToday,
      defaultDateRangeMonths: undefined,
    }) as [dayjs.Dayjs, dayjs.Dayjs];

    expect(from.format('YYYY-MM-DD')).toBe(
      dayjs().startOf('month').format('YYYY-MM-DD')
    );
  });
});
