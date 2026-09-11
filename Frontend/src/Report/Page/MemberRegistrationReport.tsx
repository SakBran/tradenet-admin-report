import { useCallback, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Row,
  Select,
  Space,
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
import { reportConfigs } from '../config/reportConfigs';
import { buildExcelPresentation } from '../excel/buildExcelPresentation';
import { enqueueExcelExport } from '../excel/excelEnqueue';
import { buildReportHeaderLines, formatDateCell } from '../reportPresentation';

/**
 * The grid is hand-built here, but the sheet is described by the shared config so
 * `buildExcelPresentation` can produce the spec the Excel queue requires — the two column
 * lists must stay identical (both mirror MemberRegistrationReport.rdlc:278-718).
 */
const config = reportConfigs.MemberRegistrationReport;

type ApplyType = 'All' | 'New' | 'Extension';

interface MemberRegistrationRow extends AnyObject {
  id: string;
  applyType: string;
  memberCode: string;
  email: string;
  fullName: string;
  mobile: string;
  nrcNo?: string | null;
  address: string;
  issuedDate?: string | null;
  startDate?: string | null;
  endDate?: string | null;
}

// Request shape posted to the backend. PascalCase because the Excel presentation spec's
// header lines are built from these same values (config.reportSubtitle reads FromDate/ToDate),
// so the page must not carry two spellings of one filter.
interface MemberRegistrationFilters {
  [key: string]: unknown;
  FromDate: string;
  ToDate: string;
  ApplyType: ApplyType;
}

interface MemberRegistrationFormValues {
  dateRange: [Dayjs, Dayjs];
  applyType: ApplyType;
}

const formatDate = (value: unknown) => formatDateCell(value, 'YYYY-MM-DD');

const toApiDate = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).format(
    'YYYY-MM-DDTHH:mm:ss'
  );

const toFilters = (
  values: MemberRegistrationFormValues
): MemberRegistrationFilters => ({
  FromDate: toApiDate(values.dateRange[0], 'start'),
  ToDate: toApiDate(values.dateRange[1], 'end'),
  ApplyType: values.applyType,
});

const buildRequest = (
  filters: MemberRegistrationFilters,
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

// The old report's 8 columns (MemberRegistrationReport.rdlc:333-718), which is also what
// reportConfigs.MemberRegistrationReport declares. The grid used to carry an Address and a
// Start Date column the old report never had, and called the last one "End Date".
const memberRegistrationColumns: BasicTableColumn<MemberRegistrationRow>[] = [
  { key: 'ApplyType', dataIndex: 'applyType', title: 'Apply Type' },
  { key: 'MemberCode', dataIndex: 'memberCode', title: 'Member Code' },
  { key: 'Email', dataIndex: 'email', title: 'Email' },
  { key: 'FullName', dataIndex: 'fullName', title: 'Full Name' },
  { key: 'Mobile', dataIndex: 'mobile', title: 'Mobile' },
  { key: 'nrcNo', dataIndex: 'nrcNo', title: 'NRC No.' },
  {
    key: 'IssuedDate',
    dataIndex: 'issuedDate',
    title: 'Issued Date',
    render: formatDate,
  },
  {
    key: 'ValidDate',
    dataIndex: 'endDate',
    title: 'Valid Date',
    render: formatDate,
  },
];

const MemberRegistrationReport = () => {
  const [form] = Form.useForm<MemberRegistrationFormValues>();
  const initialFormValues = useMemo<MemberRegistrationFormValues>(() => {
    const today = dayjs();
    return {
      dateRange: [today.startOf('month'), today],
      applyType: 'All',
    };
  }, []);

  const [filters, setFilters] = useState<MemberRegistrationFilters>(() =>
    toFilters(initialFormValues)
  );
  const [hasAppliedFilters, setHasAppliedFilters] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const fetchRows = useCallback(
    async (
      query: BasicTableQuery
    ): Promise<PaginationType<MemberRegistrationRow>> => {
      const response = await axiosInstance.post<PaginationType<MemberRegistrationRow>>(
        'MemberRegistrationReport',
        buildRequest(filters, query)
      );

      return response.data;
    },
    [filters]
  );

  const generateExcel = useCallback(
    async (query: BasicTableQuery) => {
      // Export the filters currently entered in the form so the user can
      // export without first clicking Filter. Validate first so the required
      // date range is still enforced (antd highlights invalid fields).
      let values: MemberRegistrationFormValues;
      try {
        values = await form.validateFields();
      } catch {
        return;
      }
      // Excel is an async job, not a file response: the endpoint returns a queue ticket and
      // REJECTS a request that carries no presentation spec, which is why the old blob
      // download here could only ever fail.
      const applied = toFilters(values);
      await enqueueExcelExport(
        config.excelRoute,
        buildRequest(applied, query),
        buildExcelPresentation(config, applied),
        config.excelFileName
      );
    },
    [form]
  );

  const applyFilters = (values: MemberRegistrationFormValues) => {
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

  // The legacy RDLC header block, from the same config the Excel spec uses, so the grid and
  // the sheet print the same three lines.
  const reportHeaderLines = hasAppliedFilters
    ? buildReportHeaderLines(config, filters)
    : undefined;

  return (
    <>
      <PageHeader title="Member Registration Report" />

      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={initialFormValues}
          onFinish={applyFilters}
        >
          <Row gutter={[16, 16]} align="bottom">
            <Col xs={24} md={12} lg={10}>
              <Form.Item
                label="Date Range"
                name="dateRange"
                rules={[{ required: true, message: 'Date range is required' }]}
              >
                <DatePicker.RangePicker
                  allowClear={false}
                  style={{ width: '100%' }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={8} lg={6}>
              <Form.Item label="Apply Type" name="applyType">
                <Select
                  options={[
                    { label: 'All', value: 'All' },
                    { label: 'New', value: 'New' },
                    { label: 'Extension', value: 'Extension' },
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={4} lg={8}>
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

      <BasicTable<MemberRegistrationRow>
        title="Member Registration"
        reportHeaderLines={reportHeaderLines}
        tableId="memberRegistrationReportTable"
        columns={memberRegistrationColumns}
        fetchData={fetchRows}
        onExcel={generateExcel}
        showActions={false}
        enabled={hasAppliedFilters}
        excelEnabled
        idleText="Set filters, then click Filter to load the report."
        refreshKey={refreshKey}
        initialSortColumn={config.initialSortColumn}
        rowNumberTitle="No."
        excelFileName={config.excelFileName}
      />
    </>
  );
};

export default MemberRegistrationReport;
