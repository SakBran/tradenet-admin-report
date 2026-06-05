import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Input,
  Radio,
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

const API_ROUTE = 'ListOfDirectors';
const EXCEL_ROUTE = 'ListOfDirectors/Excel';
const EXCEL_FILE_NAME = 'ListOfDirectors.xlsx';

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

// '' = no NRC filter (all). 'Current' / 'Old' mirror the legacy admin's NRCType,
// which fn_GetNRCNo uses to decide how to assemble the NRC string for matching.
type NrcMode = '' | 'Current' | 'Old';

interface DirectorRow extends AnyObject {
  companyRegistrationNo: string;
  companyName: string;
  directorName?: string | null;
  directorPosition?: string | null;
  directorNRC?: string | null;
  directorNationality?: string | null;
  directorBlackList?: string | null;
}

// Request shape posted to the backend (PascalCase keys map to the C# request DTO).
interface DirectorFilters {
  FromDate: string;
  ToDate: string;
  CompanyRegistrationNo: string;
  Name: string;
  Nationality: string;
  NRCType: NrcMode;
  NRCPrefixId: number;
  NRCPrefixCodeId: number;
  NRCNo: string;
  Type: string;
}

interface DirectorFormValues {
  dateRange: [Dayjs, Dayjs];
  CompanyRegistrationNo?: string;
  Name?: string;
  Nationality?: string;
  nrcMode: NrcMode;
  // Current NRC parts
  statePrefix?: number;
  townshipId?: number; // -> NRCPrefixId
  nrcCodeId?: number; // -> NRCPrefixCodeId
  currentNrcNo?: string;
  // Old NRC (single free-text field, e.g. "TGKN-05584")
  oldNrcNo?: string;
}

interface NrcPrefixOption {
  id: number;
  statePrefix: number;
  townshipPrefix: string;
}

interface NrcCodeOption {
  id: number;
  code: string;
  label: string;
}

type ExcelEnqueueResult = {
  status: 'Ready' | 'Queued' | 'Processing';
  jobId: string;
  fileName?: string;
  downloadUrl?: string;
  message?: string;
};

const toApiDate = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).format(
    'YYYY-MM-DDTHH:mm:ss'
  );

// Assemble the legacy filter shape from the form. The NRC fields are derived from
// the chosen Current/Old mode so the backend's fn_GetNRCNo builds the same string
// it stores against each director (Current = structured prefixes + number, Old = raw).
const toFilters = (values: DirectorFormValues): DirectorFilters => {
  const mode: NrcMode = values.nrcMode ?? '';

  let nrcPrefixId = 0;
  let nrcPrefixCodeId = 0;
  let nrcNo = '';

  if (mode === 'Current') {
    nrcPrefixId = values.townshipId ?? 0;
    nrcPrefixCodeId = values.nrcCodeId ?? 0;
    nrcNo = (values.currentNrcNo ?? '').trim();
  } else if (mode === 'Old') {
    nrcNo = (values.oldNrcNo ?? '').trim();
  }

  return {
    FromDate: toApiDate(values.dateRange[0], 'start'),
    ToDate: toApiDate(values.dateRange[1], 'end'),
    CompanyRegistrationNo: (values.CompanyRegistrationNo ?? '').trim(),
    Name: (values.Name ?? '').trim(),
    Nationality: (values.Nationality ?? '').trim(),
    NRCType: mode,
    NRCPrefixId: nrcPrefixId,
    NRCPrefixCodeId: nrcPrefixCodeId,
    NRCNo: nrcNo,
    Type: 'Director List',
  };
};

const buildRequest = (filters: DirectorFilters, query: BasicTableQuery) => ({
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

const directorColumns: BasicTableColumn<DirectorRow>[] = [
  {
    key: 'CompanyRegistrationNo',
    dataIndex: 'companyRegistrationNo',
    title: 'Company Registration No',
  },
  { key: 'CompanyName', dataIndex: 'companyName', title: 'Company Name' },
  { key: 'Name', dataIndex: 'directorName', title: 'Name' },
  { key: 'Position', dataIndex: 'directorPosition', title: 'Position' },
  { key: 'NRCNo', dataIndex: 'directorNRC', title: 'NRC No.' },
  { key: 'Nationality', dataIndex: 'directorNationality', title: 'Nationality' },
  { key: 'Status', dataIndex: 'directorBlackList', title: 'Status' },
];

const ListOfDirectors = () => {
  const [form] = Form.useForm<DirectorFormValues>();
  const initialFormValues = useMemo<DirectorFormValues>(() => {
    const today = dayjs();
    return {
      dateRange: [today.startOf('month'), today],
      CompanyRegistrationNo: '',
      Name: '',
      Nationality: '',
      nrcMode: '',
    };
  }, []);

  const [filters, setFilters] = useState<DirectorFilters>(() =>
    toFilters(initialFormValues)
  );
  const [hasAppliedFilters, setHasAppliedFilters] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const [nrcPrefixes, setNrcPrefixes] = useState<NrcPrefixOption[]>([]);
  const [nrcCodes, setNrcCodes] = useState<NrcCodeOption[]>([]);

  const nrcMode = (Form.useWatch('nrcMode', form) ?? '') as NrcMode;
  const selectedState = Form.useWatch('statePrefix', form) as
    | number
    | undefined;

  // Load NRC reference data once for the Current-NRC cascade. The dedicated
  // `nrc-prefixes` endpoint tags every township with its StatePrefix so the
  // State -> Township filtering can happen entirely client-side.
  useEffect(() => {
    let isMounted = true;

    const loadNrcData = async () => {
      try {
        const [prefixResponse, codeResponse] = await Promise.all([
          axiosInstance.get<NrcPrefixOption[]>('ReportLookups/nrc-prefixes'),
          axiosInstance.get<NrcCodeOption[]>('ReportLookups/nrcprefixcodes'),
        ]);

        if (isMounted) {
          setNrcPrefixes(prefixResponse.data ?? []);
          setNrcCodes(codeResponse.data ?? []);
        }
      } catch {
        // Leave the lists empty; the NRC dropdowns simply have no options.
      }
    };

    loadNrcData();

    return () => {
      isMounted = false;
    };
  }, []);

  const stateOptions = useMemo(() => {
    const distinct = Array.from(
      new Set(nrcPrefixes.map((prefix) => prefix.statePrefix))
    ).sort((a, b) => a - b);

    return distinct.map((statePrefix) => ({
      label: String(statePrefix),
      value: statePrefix,
    }));
  }, [nrcPrefixes]);

  const townshipOptions = useMemo(() => {
    if (selectedState === undefined || selectedState === null) {
      return [];
    }

    return nrcPrefixes
      .filter((prefix) => prefix.statePrefix === selectedState)
      .map((prefix) => ({
        label: prefix.townshipPrefix,
        value: prefix.id,
      }));
  }, [nrcPrefixes, selectedState]);

  const codeOptions = useMemo(
    () =>
      nrcCodes.map((code) => ({
        label: code.label ? `${code.code} (${code.label})` : code.code,
        value: code.id,
      })),
    [nrcCodes]
  );

  const fetchRows = useCallback(
    async (query: BasicTableQuery): Promise<PaginationType<DirectorRow>> => {
      const response = await axiosInstance.post<PaginationType<DirectorRow>>(
        API_ROUTE,
        buildRequest(filters, query)
      );

      return response.data;
    },
    [filters]
  );

  const generateExcel = useCallback(
    async (query: BasicTableQuery) => {
      // Export the filters currently entered in the form so the user can export
      // without first clicking Filter. Validate first so the required date range
      // is still enforced (antd highlights invalid fields).
      let values: DirectorFormValues;
      try {
        values = await form.validateFields();
      } catch {
        return;
      }

      const response = await axiosInstance.post<ExcelEnqueueResult>(
        EXCEL_ROUTE,
        buildRequest(toFilters(values), query)
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
        downloadBlob(blob, result.fileName ?? EXCEL_FILE_NAME);
        message.success('Your Excel export is ready and downloading.');
        return;
      }

      if (result.status === 'Processing') {
        message.info(
          'This export is already being generated. It will appear in Exports when ready.'
        );
      } else {
        message.success('Export queued. It will appear in Exports when ready.');
      }
    },
    [form]
  );

  const applyFilters = (values: DirectorFormValues) => {
    setFilters(toFilters(values));
    setHasAppliedFilters(true);
    setRefreshKey((current) => current + 1);
  };

  const resetFilters = () => {
    form.setFieldsValue(initialFormValues);
    setFilters(toFilters(initialFormValues));
    setHasAppliedFilters(false);
    setRefreshKey((current) => current + 1);
  };

  // Legacy RDLC-style report header, shown once filters are applied and reflecting
  // the chosen date range (mirrors the old admin's "Directors List (..) To (..)").
  const reportHeaderLines = hasAppliedFilters
    ? [
        'Ministry of Commerce',
        'Directorate of Trade',
        `Directors List (${dayjs(filters.FromDate).format(
          'DD/MM/YYYY'
        )}) To (${dayjs(filters.ToDate).format('DD/MM/YYYY')})`,
      ]
    : undefined;

  return (
    <>
      <PageHeader title="List of Directors" />

      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={initialFormValues}
          onFinish={applyFilters}
        >
          <Row gutter={[16, 16]} align="bottom">
            <Col xs={24} md={12} lg={8}>
              <Form.Item
                label="From Date / To Date"
                name="dateRange"
                rules={[{ required: true, message: 'Date range is required' }]}
              >
                <DatePicker.RangePicker
                  allowClear={false}
                  style={{ width: '100%' }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item label="Company Registration No" name="CompanyRegistrationNo">
                <Input allowClear placeholder="e.g. 143258106" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item label="Name" name="Name">
                <Input allowClear placeholder="Director name" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item label="Nationality" name="Nationality">
                <Input allowClear placeholder="e.g. Myanmar" />
              </Form.Item>
            </Col>
            <Col xs={24} lg={16}>
              <Form.Item label="NRC" name="nrcMode">
                <Radio.Group>
                  <Radio value="">All</Radio>
                  <Radio value="Current">Current NRC</Radio>
                  <Radio value="Old">Old NRC</Radio>
                </Radio.Group>
              </Form.Item>
            </Col>
          </Row>

          {nrcMode === 'Current' && (
            <Row gutter={[16, 16]} align="bottom">
              <Col xs={24} md={12} lg={6}>
                <Form.Item label="State / Region" name="statePrefix">
                  <Select
                    showSearch
                    allowClear
                    optionFilterProp="label"
                    placeholder="Select state/region"
                    options={stateOptions}
                    onChange={() => form.setFieldValue('townshipId', undefined)}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} md={12} lg={6}>
                <Form.Item label="Township" name="townshipId">
                  <Select
                    showSearch
                    allowClear
                    optionFilterProp="label"
                    placeholder={
                      selectedState === undefined
                        ? 'Select a state/region first'
                        : 'Select township'
                    }
                    disabled={selectedState === undefined}
                    options={townshipOptions}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} md={12} lg={6}>
                <Form.Item label="NRC Code" name="nrcCodeId">
                  <Select
                    showSearch
                    allowClear
                    optionFilterProp="label"
                    placeholder="Select code"
                    options={codeOptions}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} md={12} lg={6}>
                <Form.Item label="NRC No" name="currentNrcNo">
                  <Input
                    allowClear
                    inputMode="numeric"
                    maxLength={6}
                    placeholder="e.g. 193055"
                  />
                </Form.Item>
              </Col>
            </Row>
          )}

          {nrcMode === 'Old' && (
            <Row gutter={[16, 16]} align="bottom">
              <Col xs={24} md={12} lg={8}>
                <Form.Item label="Old NRC" name="oldNrcNo">
                  <Input allowClear placeholder="e.g. TGKN-05584" />
                </Form.Item>
              </Col>
            </Row>
          )}

          <Row gutter={[16, 16]} align="bottom">
            <Col xs={24}>
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
            </Col>
          </Row>
        </Form>
      </Card>

      <BasicTable<DirectorRow>
        title="List of Directors"
        reportHeaderLines={reportHeaderLines}
        tableId="listOfDirectorsTable"
        columns={directorColumns}
        fetchData={fetchRows}
        onExcel={generateExcel}
        showActions={false}
        enabled={hasAppliedFilters}
        excelEnabled
        idleText="Set filters, then click Filter to load the report."
        refreshKey={refreshKey}
        excelFileName={EXCEL_FILE_NAME}
        rowNumberTitle="No."
      />
    </>
  );
};

export default ListOfDirectors;
