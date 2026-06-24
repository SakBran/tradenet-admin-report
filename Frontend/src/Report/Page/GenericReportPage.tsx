import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Space,
  message,
} from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs, { Dayjs } from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { PageHeader } from '../../components';
import {
  BasicTable,
  BasicTableColumn,
  BasicTableQuery,
} from '../../components/My Components/Table/BasicTable';
import { AnyObject } from '../../types/AnyObject';
import { PaginationType } from '../../types/PaginationType';
import {
  ReportColumnConfig,
  ReportColumnDrilldown,
  ReportFilterConfig,
  ReportPageConfig,
} from '../config/reportTypes';

type ExcelEnqueueResult = {
  status: 'Ready' | 'Queued' | 'Processing';
  jobId: string;
  fileName?: string;
  downloadUrl?: string;
  message?: string;
};

type FilterValue =
  | string
  | number
  | boolean
  | Dayjs
  | [Dayjs, Dayjs]
  | undefined;
type FilterFormValues = Record<string, FilterValue>;

const formTypePrefixes: Array<[string, string]> = [
  ['BorderExportLicence', 'Border Export Licence'],
  ['BorderImportLicence', 'Border Import Licence'],
  ['BorderExportPermit', 'Border Export Permit'],
  ['BorderImportPermit', 'Border Import Permit'],
  ['ExportLicence', 'Export Licence'],
  ['ImportLicence', 'Import Licence'],
  ['ExportPermit', 'Export Permit'],
  ['ImportPermit', 'Import Permit'],
];

interface LookupOption {
  id: number;
  code: string;
  label: string;
  value?: string | number;
  /** Parent id for cascading lookups (e.g. an OGA Section's OGADepartmentId). */
  parentId?: number;
}

interface LookupFilterConfig {
  lookupName: string;
  label: string;
}

interface CompanyNameLookupResult {
  companyRegistrationNo: string;
  companyName: string;
}

const idFilterLookups: Record<string, LookupFilterConfig> = {
  AmendRemarkId: { lookupName: 'amendRemarks', label: 'Amend Remark' },
  BusinessTypeId: { lookupName: 'businessTypes', label: 'Business Type' },
  BuyerCountryId: { lookupName: 'countries', label: 'Buyer Country' },
  ChequeNoId: { lookupName: 'chequeNos', label: 'Cheque No' },
  ExportImportIncotermId: {
    lookupName: 'exportImportIncoterms',
    label: 'Export Import Incoterm',
  },
  ExportImportMethodId: {
    lookupName: 'exportImportMethods',
    label: 'Export Import Method',
  },
  ExportImportSectionId: {
    lookupName: 'exportImportSections',
    label: 'Export Import Section',
  },
  LineofBusinessId: {
    lookupName: 'lineofBusinesses',
    label: 'Lineof Business',
  },
  NRCPrefixCodeId: { lookupName: 'nrcprefixCodes', label: 'NRC Prefix Code' },
  NRCPrefixId: { lookupName: 'nrcprefixes', label: 'NRC Prefix' },
  OGADepartmentId: { lookupName: 'ogaDepartments', label: 'OGA Department' },
  OGASectionId: { lookupName: 'ogaSections', label: 'OGA Section' },
  PaThaKaTypeId: { lookupName: 'paThaKaTypes', label: 'PaThaKa Type' },
  SakhanId: { lookupName: 'sakhans', label: 'Sakhan' },
  SellerCountryId: { lookupName: 'countries', label: 'Seller Country' },
};

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const formatDate = (value: unknown) => {
  if (!value) {
    return 'N/A';
  }

  const parsed = dayjs(value.toString());
  return parsed.isValid() ? parsed.format('YYYY-MM-DD') : value.toString();
};

const formatBoolean = (value: unknown) => {
  if (value === true || value?.toString().toLowerCase() === 'true') {
    return 'Yes';
  }

  if (value === false || value?.toString().toLowerCase() === 'false') {
    return 'No';
  }

  return value?.toString() ?? 'N/A';
};

const formatMoney = (value: unknown) => {
  const parsed = Number(value?.toString().replace(/,/g, ''));
  return Number.isFinite(parsed) ? parsed.toFixed(2) : value?.toString() ?? 'N/A';
};

const toTransactionAmountNumber = (value: unknown) => {
  const raw = value?.toString().replace(/,/g, '').trim() ?? '';
  if (!raw) {
    return 0;
  }

  if (raw.includes('.')) {
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  const wholePart = raw.length > 2 ? raw.slice(0, -2) : '0';
  const decimalPart = raw.slice(-2).padStart(2, '0');
  const parsed = Number(`${Number(wholePart)}.${decimalPart}`);
  return Number.isFinite(parsed) ? parsed : 0;
};

const formatTransactionAmount = (value: unknown) =>
  toTransactionAmountNumber(value).toFixed(2);

const toMoneyNumber = (value: unknown) => {
  const parsed = Number(value?.toString().replace(/,/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
};

const getMpuAmount = (row: AnyObject) =>
  toTransactionAmountNumber(row.transactionAmount) -
  toMoneyNumber(row.mocAmount) -
  toMoneyNumber(row.imAmount);

const getAmountDiff = (row: AnyObject) =>
  toTransactionAmountNumber(row.transactionAmount) -
  toMoneyNumber(row.mocAmount);

const hasValue = (value: unknown) =>
  value !== undefined && value !== null && value.toString().trim() !== '';

const formatColumnValue = (
  value: unknown,
  dataType?: ReportColumnConfig['dataType']
) => {
  if (!hasValue(value)) {
    return 'N/A';
  }

  if (dataType === 'date') {
    return formatDate(value);
  }

  if (dataType === 'boolean') {
    return formatBoolean(value);
  }

  if (dataType === 'money') {
    return formatMoney(value);
  }

  return value?.toString() ?? 'N/A';
};

const toApiDate = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).format(
    'YYYY-MM-DDTHH:mm:ss'
  );

const getInitialFilterValue = (filter: ReportFilterConfig): FilterValue => {
  if (filter.type === 'dateRange') {
    const today = dayjs();
    const months = Math.max(1, filter.defaultDateRangeMonths ?? 1);
    if (months > 1) {
      return [today.subtract(months - 1, 'month').startOf('month'), today];
    }

    return [today.startOf('month'), today];
  }

  if (filter.type === 'date') {
    return dayjs();
  }

  if (filter.defaultValue !== undefined) {
    return filter.defaultValue;
  }

  if (filter.type === 'number') {
    return 0;
  }

  if (filter.type === 'boolean') {
    return false;
  }

  return '';
};

const buildInitialValues = (filters: ReportFilterConfig[]) =>
  filters.reduce<FilterFormValues>((values, filter) => {
    values[filter.name] = getInitialFilterValue(filter);
    return values;
  }, {});

const getDerivedFilterValues = (
  controllerName: string,
  filters: ReportFilterConfig[]
) => {
  const values: FilterFormValues = {};
  const hasFilter = (name: string) =>
    filters.some((filter) => filter.name === name);
  const formType = formTypePrefixes.find(([prefix]) =>
    controllerName.startsWith(prefix)
  )?.[1];

  if (formType && hasFilter('FormType')) {
    values.FormType = formType;
  }

  if (hasFilter('Type')) {
    if (controllerName.startsWith('Border')) {
      values.Type = 'Border';
    } else if (
      formType &&
      ['Export Licence', 'Import Licence', 'Export Permit', 'Import Permit'].includes(
        formType
      )
    ) {
      values.Type = 'Oversea';
    }
  }

  return values;
};

const normalizeFilters = (
  filters: ReportFilterConfig[],
  values: FilterFormValues
) =>
  filters.reduce<Record<string, unknown>>((request, filter) => {
    if (filter.excludeFromRequest) {
      return request;
    }

    const value = values[filter.name];

    if (filter.type === 'dateRange') {
      const range = value as [Dayjs, Dayjs] | undefined;
      request[filter.fromName ?? 'FromDate'] = range?.[0]
        ? toApiDate(range[0], 'start')
        : undefined;
      request[filter.toName ?? 'ToDate'] = range?.[1]
        ? toApiDate(range[1], 'end')
        : undefined;
      return request;
    }

    if (filter.type === 'date') {
      const date = value as Dayjs | undefined;
      request[filter.name] = date ? toApiDate(date, 'start') : undefined;
      return request;
    }

    if (filter.type === 'number') {
      request[filter.name] =
        typeof value === 'number' ? value : Number(value ?? 0);
      return request;
    }

    request[filter.name] = value ?? filter.defaultValue ?? '';
    return request;
  }, {});

const buildRequest = (
  filters: Record<string, unknown>,
  query: BasicTableQuery
) => ({
  ...filters,
  pageIndex: query.pageIndex,
  pageSize: query.pageSize,
  sortColumn: query.sortColumn,
  sortOrder: query.sortOrder.toUpperCase(),
  filterColumn: query.filterColumn,
  filterQuery: query.filterQuery,
  includeTotalCount: query.includeTotalCount,
});

const downloadBlob = (blob: Blob, fileName: string) => {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
};

const toTableColumn = (
  column: ReportColumnConfig
): BasicTableColumn<AnyObject> => {
  if (column.dataIndex === 'transactionAmount' && column.dataType === 'money') {
    return { ...column, render: formatTransactionAmount };
  }

  if (column.dataIndex === 'mpuAmount') {
    return {
      ...column,
      render: (value, row) =>
        formatMoney(hasValue(value) ? value : getMpuAmount(row)),
    };
  }

  if (column.dataIndex === 'amountDiff') {
    return {
      ...column,
      render: (value, row) =>
        formatMoney(hasValue(value) ? value : getAmountDiff(row)),
    };
  }

  if (column.fallbackDataIndexes?.length) {
    return {
      ...column,
      render: (value, row) => {
        if (hasValue(value)) {
          return formatColumnValue(value, column.dataType);
        }

        const fallbackValue = column.fallbackDataIndexes
          ?.map((dataIndex) => row[dataIndex])
          .filter(hasValue)
          .join(', ');

        return formatColumnValue(fallbackValue, column.dataType);
      },
    };
  }

  if (column.dataType === 'date') {
    return { ...column, render: formatDate };
  }

  if (column.dataType === 'boolean') {
    return { ...column, render: formatBoolean };
  }

  if (column.dataType === 'money') {
    return { ...column, render: formatMoney };
  }

  return column;
};

const getLookupFilter = (filter: ReportFilterConfig) => {
  if (filter.lookupName) {
    return {
      lookupName: filter.lookupName,
      label: filter.lookupLabel ?? filter.label,
    };
  }

  return filter.name.endsWith('Id') ? idFilterLookups[filter.name] : undefined;
};

const toLookupSelectOptions = (
  options: LookupOption[] = [],
  allValue: string | number = 0
) => [
  { label: 'All', value: allValue },
  ...options.map((option) => ({
    label: option.code ? `${option.label} (${option.code})` : option.label,
    value: option.value ?? option.id,
  })),
];

const renderFilter = (
  filter: ReportFilterConfig,
  lookupOptions: Record<string, LookupOption[]>,
  loadingLookupNames: Set<string>,
  // For a cascading (dependsOn) filter, the already-narrowed option list to use
  // instead of the full lookup. Undefined for ordinary filters.
  overrideOptions?: LookupOption[]
) => {
  const lookup = getLookupFilter(filter);

  if (lookup) {
    return (
      <Select
        showSearch
        loading={loadingLookupNames.has(lookup.lookupName)}
        optionFilterProp="label"
        options={toLookupSelectOptions(
          overrideOptions ?? lookupOptions[lookup.lookupName],
          typeof filter.defaultValue === 'string' ? filter.defaultValue : 0
        )}
      />
    );
  }

  if (filter.type === 'dateRange') {
    return (
      <DatePicker.RangePicker
        allowClear={false}
        placeholder={[
          filter.fromLabel ?? 'From Date',
          filter.toLabel ?? 'To Date',
        ]}
        style={{ width: '100%' }}
      />
    );
  }

  if (filter.type === 'date') {
    return <DatePicker allowClear={false} style={{ width: '100%' }} />;
  }

  if (filter.type === 'number') {
    return <InputNumber min={0} style={{ width: '100%' }} />;
  }

  if (filter.type === 'select') {
    return (
      <Select
        showSearch
        optionFilterProp="label"
        options={filter.options ?? []}
      />
    );
  }

  if (filter.type === 'boolean') {
    return (
      <Select
        options={[
          { label: 'No', value: false },
          { label: 'Yes', value: true },
        ]}
      />
    );
  }

  if (filter.type === 'readonlyText') {
    return <Input readOnly />;
  }

  return <Input />;
};

interface GenericReportPageProps {
  config: ReportPageConfig;
}

const GenericReportPage = ({ config }: GenericReportPageProps) => {
  const [form] = Form.useForm<FilterFormValues>();
  const navigate = useNavigate();
  const location = useLocation();
  const lastDrillRef = useRef<string | undefined>(undefined);
  const derivedFilterValues = useMemo(
    () => getDerivedFilterValues(config.controllerName, config.filters),
    [config.controllerName, config.filters]
  );
  const initialFormValues = useMemo(
    () => ({
      ...buildInitialValues(config.filters),
      ...derivedFilterValues,
    }),
    [config.filters, derivedFilterValues]
  );
  const visibleFilters = useMemo(
    () =>
      config.filters.filter(
        (filter) => derivedFilterValues[filter.name] === undefined
      ),
    [config.filters, derivedFilterValues]
  );
  const normalizeReportFilters = useCallback(
    (values: FilterFormValues) =>
      normalizeFilters(config.filters, {
        ...values,
        ...derivedFilterValues,
      }),
    [config.filters, derivedFilterValues]
  );
  const [filters, setFilters] = useState<Record<string, unknown>>(() =>
    normalizeReportFilters(initialFormValues)
  );
  const [hasAppliedFilters, setHasAppliedFilters] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [lookupOptions, setLookupOptions] = useState<
    Record<string, LookupOption[]>
  >({});
  const [loadingLookupNames, setLoadingLookupNames] = useState<Set<string>>(
    () => new Set()
  );
  // Cascading filters (e.g. OGA Section depends on OGA Department): a dependent
  // select only lists lookup options whose parentId matches the parent's value.
  const dependentFilters = useMemo(
    () => config.filters.filter((filter) => filter.dependsOn),
    [config.filters]
  );
  const parentFilterNames = useMemo(
    () =>
      Array.from(
        new Set(
          dependentFilters
            .map((filter) => filter.dependsOn)
            .filter((name): name is string => Boolean(name))
        )
      ),
    [dependentFilters]
  );
  const [parentFilterValues, setParentFilterValues] = useState<
    Record<string, unknown>
  >({});
  const companyNameFilter = useMemo(
    () =>
      config.filters.find(
        (filter) => filter.populateFromCompanyRegistrationNo
      ),
    [config.filters]
  );
  const watchedCompanyRegistrationNo = Form.useWatch(
    'CompanyRegistrationNo',
    form
  );
  const resolvedColumns = useMemo(
    () =>
      config.resolveColumns
        ? config.resolveColumns(filters, config.columns)
        : config.columns,
    [config, filters]
  );
  const tableColumns = useMemo(
    () => resolvedColumns.map(toTableColumn),
    [resolvedColumns]
  );
  const legacyReportViewer =
    config.legacyReportViewer ??
    (config.controllerName.startsWith('ImportLicence') ||
      config.controllerName.startsWith('BorderImportPermit'));

  const reportLookupFilters = useMemo(() => {
    const lookups = config.filters
      .map(getLookupFilter)
      .filter((lookup): lookup is LookupFilterConfig => Boolean(lookup));

    return Array.from(
      new Map(lookups.map((lookup) => [lookup.lookupName, lookup])).values()
    );
  }, [config.filters]);

  useEffect(() => {
    let isMounted = true;
    const missingLookups = reportLookupFilters.filter(
      (lookup) => !lookupOptions[lookup.lookupName]
    );

    if (!missingLookups.length) {
      return;
    }

    setLoadingLookupNames((current) => {
      const next = new Set(current);
      missingLookups.forEach((lookup) => next.add(lookup.lookupName));
      return next;
    });

    const loadLookups = async () => {
      const responses = await Promise.all(
        missingLookups.map(async (lookup) => {
          const response = await axiosInstance.get<LookupOption[]>(
            `ReportLookups/${lookup.lookupName}`
          );

          return [lookup.lookupName, response.data] as const;
        })
      );

      if (!isMounted) {
        return;
      }

      setLookupOptions((current) => ({
        ...current,
        ...Object.fromEntries(responses),
      }));
      setLoadingLookupNames((current) => {
        const next = new Set(current);
        missingLookups.forEach((lookup) => next.delete(lookup.lookupName));
        return next;
      });
    };

    loadLookups().catch(() => {
      if (!isMounted) {
        return;
      }

      setLoadingLookupNames((current) => {
        const next = new Set(current);
        missingLookups.forEach((lookup) => next.delete(lookup.lookupName));
        return next;
      });
    });

    return () => {
      isMounted = false;
    };
  }, [lookupOptions, reportLookupFilters]);

  useEffect(() => {
    if (!companyNameFilter) {
      return;
    }

    const registrationNo = String(watchedCompanyRegistrationNo ?? '').trim();

    if (registrationNo === '') {
      form.setFieldValue(companyNameFilter.name, '');
      return;
    }

    let isCancelled = false;
    const timeoutId = window.setTimeout(async () => {
      try {
        const response = await axiosInstance.get<CompanyNameLookupResult>(
          'ReportLookups/company-name',
          {
            params: { companyRegistrationNo: registrationNo },
          }
        );

        if (!isCancelled) {
          form.setFieldValue(
            companyNameFilter.name,
            response.data.companyName ?? ''
          );
        }
      } catch {
        if (!isCancelled) {
          form.setFieldValue(companyNameFilter.name, '');
        }
      }
    }, 300);

    return () => {
      isCancelled = true;
      window.clearTimeout(timeoutId);
    };
  }, [companyNameFilter, form, watchedCompanyRegistrationNo]);

  const fetchRows = useCallback(
    async (query: BasicTableQuery): Promise<PaginationType<AnyObject>> => {
      const response = await axiosInstance.post<PaginationType<AnyObject>>(
        config.apiRoute,
        buildRequest(filters, query)
      );

      return response.data;
    },
    [config.apiRoute, filters]
  );

  const generateExcel = useCallback(
    async (query: BasicTableQuery) => {
      // Excel is now asynchronous: the endpoint enqueues a job (or returns an
      // already-finished file to reuse). See docs/ExcelJobQueueTask.md.
      // Export the filters currently entered in the form so the user can
      // export without first clicking Filter to load the grid. Validate first
      // so required filters are still enforced (antd highlights invalid fields).
      let values: FilterFormValues;
      try {
        values = await form.validateFields();
      } catch {
        return;
      }
      const currentFilters = normalizeReportFilters(values);
      const response = await axiosInstance.post<ExcelEnqueueResult>(
        config.excelRoute,
        buildRequest(currentFilters, query)
      );
      const result = response.data;

      if (result.status === 'Ready' && result.downloadUrl) {
        const fileResponse = await axiosInstance.get(result.downloadUrl, {
          responseType: 'blob',
        });
        const blob = new Blob([fileResponse.data], {
          type: String(
            fileResponse.headers['content-type'] ?? excelContentType
          ),
        });
        downloadBlob(blob, result.fileName ?? config.excelFileName);
        message.success('Your Excel export is ready and downloading.');
        return;
      }

      if (result.status === 'Processing') {
        message.info(
          'This export is already being generated. It will appear in Exports when ready.'
        );
      } else {
        message.success(
          'Export queued. It will appear in Exports when ready.'
        );
      }
    },
    [config.excelFileName, config.excelRoute, form]
  );

  const applyFilters = (values: FilterFormValues) => {
    setFilters(normalizeReportFilters(values));
    setHasAppliedFilters(true);
    setRefreshKey((current) => current + 1);
  };

  const resetFilters = () => {
    form.setFieldsValue(initialFormValues);
    setFilters(normalizeReportFilters(initialFormValues));
    setParentFilterValues({});
    setHasAppliedFilters(false);
    setRefreshKey((current) => current + 1);
  };

  // When a parent filter (e.g. OGA Department) changes, reset its dependent
  // children (e.g. OGA Section) and record the parent value so the child's
  // option list re-narrows. setFieldValue does not re-trigger onValuesChange,
  // so this cannot loop.
  const handleValuesChange = (
    changedValues: Partial<FilterFormValues>,
    allValues: FilterFormValues
  ) => {
    const parentChanged = parentFilterNames.some((name) =>
      Object.prototype.hasOwnProperty.call(changedValues, name)
    );
    if (!parentChanged) {
      return;
    }

    dependentFilters.forEach((filter) => {
      if (
        filter.dependsOn &&
        Object.prototype.hasOwnProperty.call(changedValues, filter.dependsOn)
      ) {
        form.setFieldValue(filter.name, filter.defaultValue ?? 0);
      }
    });

    setParentFilterValues((current) => {
      const next = { ...current };
      parentFilterNames.forEach((name) => {
        next[name] = (allValues as Record<string, unknown>)[name];
      });
      return next;
    });
  };

  // Narrowed option list for a cascading filter (undefined for ordinary filters).
  const getDependentLookupOptions = (
    filter: ReportFilterConfig
  ): LookupOption[] | undefined => {
    if (!filter.dependsOn) {
      return undefined;
    }
    const lookup = getLookupFilter(filter);
    if (!lookup) {
      return undefined;
    }
    const allOptions = lookupOptions[lookup.lookupName] ?? [];
    const parentId = Number(parentFilterValues[filter.dependsOn]);
    if (!parentId) {
      // Parent unset ("All") → show every option.
      return allOptions;
    }
    return allOptions.filter((option) => option.parentId === parentId);
  };

  // Drill-down: navigate to a target report carrying the clicked row's params
  // plus selected current filters (mirrors the legacy RDLC "blue cell" links).
  const handleDrill = useCallback(
    (drilldown: ReportColumnDrilldown, row: AnyObject) => {
      const params: Record<string, unknown> = { ...(drilldown.staticParams ?? {}) };
      (drilldown.carryFilters ?? []).forEach((name) => {
        if (filters[name] !== undefined) {
          params[name] = filters[name];
        }
      });
      Object.entries(drilldown.rowParams ?? {}).forEach(([target, rowKey]) => {
        params[target] = row[rowKey];
      });
      if (drilldown.openInNewTab) {
        // Router state does not survive window.open, so carry the params in the URL.
        const url = `/Report/${drilldown.targetReportKey}?drill=${encodeURIComponent(
          JSON.stringify(params)
        )}`;
        window.open(url, '_blank', 'noopener');
        return;
      }
      navigate(`/Report/${drilldown.targetReportKey}`, {
        state: { drillFilters: params },
      });
    },
    [filters, navigate]
  );

  // When this page is opened as a drill target, seed + auto-apply the incoming
  // filters once. The form shows the filters that exist on this report; the
  // request carries every drill param (even ones without a visible filter box,
  // e.g. SellerCountryId / CompanyRegistrationNo).
  useEffect(() => {
    // Same-tab drills carry params in router state; new-tab drills (openInNewTab)
    // carry them in the `?drill=<json>` query string since state is lost on a fresh load.
    let drill = (
      location.state as { drillFilters?: Record<string, unknown> } | null
    )?.drillFilters;
    if (!drill) {
      const raw = new URLSearchParams(location.search).get('drill');
      if (raw) {
        try {
          drill = JSON.parse(raw) as Record<string, unknown>;
        } catch {
          drill = undefined;
        }
      }
    }
    if (!drill) {
      return;
    }
    // Dedup by value (a URL-parsed object is a new reference each render, so identity
    // comparison would re-apply forever).
    const drillSig = JSON.stringify(drill);
    if (drillSig === lastDrillRef.current) {
      return;
    }
    lastDrillRef.current = drillSig;

    const formSeed: FilterFormValues = {};
    const dateRangeFilter = config.filters.find(
      (filter) => filter.type === 'dateRange'
    );
    if (dateRangeFilter) {
      const fromName = dateRangeFilter.fromName ?? 'FromDate';
      const toName = dateRangeFilter.toName ?? 'ToDate';
      if (drill[fromName] && drill[toName]) {
        formSeed[dateRangeFilter.name] = [
          dayjs(String(drill[fromName])),
          dayjs(String(drill[toName])),
        ];
      }
    }
    config.filters.forEach((filter) => {
      if (filter.type === 'dateRange') {
        return;
      }
      if (drill[filter.name] !== undefined) {
        formSeed[filter.name] = drill[filter.name] as FilterValue;
      }
    });
    form.setFieldsValue(formSeed);

    setFilters({ ...derivedFilterValues, ...drill });
    setHasAppliedFilters(true);
    setRefreshKey((current) => current + 1);
  }, [location, config.filters, derivedFilterValues, form]);

  // Legacy RDLC-style report header rendered inside the grid, shown only once
  // filters are applied. Reflects the applied Type/Date via reportSubtitle.
  const reportHeaderLines =
    hasAppliedFilters && (config.reportHeading?.length || config.reportSubtitle)
      ? [
          ...(config.reportHeading ?? []),
          ...(config.reportSubtitle ? [config.reportSubtitle(filters)] : []),
        ]
      : undefined;

  return (
    <>
      <PageHeader title={config.title} />

      {visibleFilters.length > 0 && (
        <Card>
          <Form
            form={form}
            layout="vertical"
            initialValues={initialFormValues}
            onFinish={applyFilters}
            onValuesChange={handleValuesChange}
          >
            <Row gutter={[16, 16]} align="bottom">
              {visibleFilters.map((filter) => (
                <Col xs={24} md={12} lg={6} key={filter.name}>
                  <Form.Item
                    label={filter.label ?? getLookupFilter(filter)?.label}
                    name={filter.name}
                    rules={
                      filter.required
                        ? [
                            {
                              required: true,
                              message: `${filter.label} is required`,
                            },
                          ]
                        : undefined
                    }
                  >
                    {renderFilter(
                      filter,
                      lookupOptions,
                      loadingLookupNames,
                      getDependentLookupOptions(filter)
                    )}
                  </Form.Item>
                </Col>
              ))}
              <Col xs={24} md={12} lg={6}>
                <Form.Item>
                  <Space wrap>
                    <Button
                      type="primary"
                      htmlType="submit"
                      icon={<SearchOutlined />}
                    >
                      Filter
                    </Button>
                    <Button onClick={resetFilters} icon={<ReloadOutlined />}>
                      Reset
                    </Button>
                  </Space>
                </Form.Item>
              </Col>
            </Row>
          </Form>
        </Card>
      )}

      <BasicTable<AnyObject>
        title={config.title}
        reportHeaderLines={reportHeaderLines}
        currencyTotalsColumns={config.currencyTotalsColumns}
        onDrill={handleDrill}
        tableId={`${config.controllerName}Table`}
        columns={tableColumns}
        fetchData={fetchRows}
        onExcel={generateExcel}
        showActions={false}
        enabled={hasAppliedFilters}
        excelEnabled
        idleText="Set filters, then click Filter to load the report."
        refreshKey={refreshKey}
        initialSortColumn={config.initialSortColumn}
        initialSortOrder="desc"
        lazyTotalCount={!config.disableLazyTotalCount}
        excelFileName={config.excelFileName}
        showRowNumber={config.showRowNumber ?? true}
        rowNumberTitle={legacyReportViewer ? 'No.' : 'No'}
        legacyReportViewer={legacyReportViewer}
      />
    </>
  );
};

export default GenericReportPage;
