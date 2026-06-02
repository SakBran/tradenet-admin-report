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

interface MemberRegistrationFilters {
  fromDate: string;
  toDate: string;
  applyType: ApplyType;
}

interface MemberRegistrationFormValues {
  dateRange: [Dayjs, Dayjs];
  applyType: ApplyType;
}

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const formatDate = (value: unknown) => {
  if (!value) {
    return 'N/A';
  }

  const parsed = dayjs(value.toString());
  return parsed.isValid() ? parsed.format('YYYY-MM-DD') : value.toString();
};

const toApiDate = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).format(
    'YYYY-MM-DDTHH:mm:ss'
  );

const toFilters = (
  values: MemberRegistrationFormValues
): MemberRegistrationFilters => ({
  fromDate: toApiDate(values.dateRange[0], 'start'),
  toDate: toApiDate(values.dateRange[1], 'end'),
  applyType: values.applyType,
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

const memberRegistrationColumns: BasicTableColumn<MemberRegistrationRow>[] = [
  { key: 'ApplyType', dataIndex: 'applyType', title: 'Apply Type' },
  { key: 'MemberCode', dataIndex: 'memberCode', title: 'Member Code' },
  { key: 'Email', dataIndex: 'email', title: 'Email' },
  { key: 'FullName', dataIndex: 'fullName', title: 'Full Name' },
  { key: 'Mobile', dataIndex: 'mobile', title: 'Mobile' },
  { key: 'NRCNo', dataIndex: 'nrcNo', title: 'NRC No' },
  { key: 'Address', dataIndex: 'address', title: 'Address' },
  {
    key: 'IssuedDate',
    dataIndex: 'issuedDate',
    title: 'Issued Date',
    render: formatDate,
  },
  {
    key: 'StartDate',
    dataIndex: 'startDate',
    title: 'Start Date',
    render: formatDate,
  },
  {
    key: 'EndDate',
    dataIndex: 'endDate',
    title: 'End Date',
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
      const response = await axiosInstance.post(
        'MemberRegistrationReport/Excel',
        buildRequest(toFilters(values), query),
        { responseType: 'blob' }
      );
      const blob = new Blob([response.data], {
        type: String(response.headers['content-type'] ?? excelContentType),
      });

      downloadBlob(blob, 'MemberRegistrationReport.xlsx');
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
        tableId="memberRegistrationReportTable"
        columns={memberRegistrationColumns}
        fetchData={fetchRows}
        onExcel={generateExcel}
        showActions={false}
        enabled={hasAppliedFilters}
        excelEnabled
        idleText="Set filters, then click Filter to load the report."
        refreshKey={refreshKey}
        initialSortColumn="IssuedDate"
        initialSortOrder="desc"
        excelFileName="MemberRegistrationReport.xlsx"
      />
    </>
  );
};

export default MemberRegistrationReport;
