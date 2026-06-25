import { useCallback, useContext, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Input,
  Row,
  Select,
  Space,
  Tooltip,
  Typography,
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
import AuthContext from '../../context/AuthContext';

type FormValues = {
  dateRange: [Dayjs, Dayjs];
  userId?: string;
  eventType?: string;
};

type AppliedFilters = {
  fromDate?: string;
  toDate?: string;
  userId?: string;
  eventType?: string;
};

const eventTypeOptions = [
  { label: 'All events', value: '' },
  { label: 'Sign in', value: 'SignIn' },
  { label: 'Sign in failed', value: 'SignInFailed' },
  { label: 'API request', value: 'ApiRequest' },
  { label: 'Navigation', value: 'Navigation' },
  { label: 'Logout', value: 'Logout' },
  { label: 'Click', value: 'Click' },
];

// Timestamps are stored in UTC; convert the picked local day boundaries to UTC instants.
const toUtcIso = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).toISOString();

const buildFilters = (values: FormValues): AppliedFilters => {
  const [from, to] = values.dateRange ?? [];
  return {
    fromDate: from ? toUtcIso(from, 'start') : undefined,
    toDate: to ? toUtcIso(to, 'end') : undefined,
    userId: values.userId?.trim() || undefined,
    eventType: values.eventType || undefined,
  };
};

const buildRequest = (filters: AppliedFilters, query: BasicTableQuery) => ({
  FromDate: filters.fromDate,
  ToDate: filters.toDate,
  UserId: filters.userId,
  EventType: filters.eventType,
  pageIndex: query.pageIndex,
  pageSize: query.pageSize,
  sortColumn: query.sortColumn,
  sortOrder: query.sortOrder.toUpperCase(),
  filterColumn: query.filterColumn,
  filterQuery: query.filterQuery,
  includeTotalCount: query.includeTotalCount,
});

const DetailsCell = ({ value }: { value: string | null | undefined }) => {
  if (!value) {
    return <span style={{ color: '#bbb' }}>—</span>;
  }

  const preview = value.length > 80 ? `${value.slice(0, 80)}…` : value;
  return (
    <Tooltip
      title={
        <pre style={{ margin: 0, whiteSpace: 'pre-wrap', maxWidth: 480 }}>
          {value}
        </pre>
      }
    >
      <Typography.Text code style={{ cursor: 'help' }}>
        {preview}
      </Typography.Text>
    </Tooltip>
  );
};

const columns: BasicTableColumn<AnyObject>[] = [
  {
    key: 'timestampUtc',
    dataIndex: 'timestampUtc',
    title: 'Time (UTC)',
    sortable: true,
    width: 170,
    render: (value) =>
      value ? dayjs(String(value)).format('YYYY-MM-DD HH:mm:ss') : '',
  },
  {
    key: 'userName',
    dataIndex: 'userName',
    title: 'User',
    render: (value, row) =>
      (value as string) || (row.userId as string) || 'Anonymous',
  },
  { key: 'eventType', dataIndex: 'eventType', title: 'Event', width: 130 },
  { key: 'source', dataIndex: 'source', title: 'Source', width: 90 },
  { key: 'httpMethod', dataIndex: 'httpMethod', title: 'Method', width: 90 },
  { key: 'path', dataIndex: 'path', title: 'Path' },
  { key: 'ipAddress', dataIndex: 'ipAddress', title: 'IP', width: 130 },
  {
    key: 'statusCode',
    dataIndex: 'statusCode',
    title: 'Status',
    width: 90,
    sortable: true,
  },
  {
    key: 'durationMs',
    dataIndex: 'durationMs',
    title: 'Duration (ms)',
    width: 130,
    sortable: true,
  },
  {
    key: 'detailsJson',
    dataIndex: 'detailsJson',
    title: 'Details',
    render: (value) => <DetailsCell value={value as string | null} />,
  },
];

const ActivityLogList = () => {
  const [form] = Form.useForm<FormValues>();
  const auth = useContext(AuthContext);
  const isAdmin = auth?.user?.permission === 'Admin';

  const initialValues = useMemo<FormValues>(
    () => ({
      dateRange: [dayjs().subtract(6, 'day').startOf('day'), dayjs()],
      userId: '',
      eventType: '',
    }),
    []
  );

  const [filters, setFilters] = useState<AppliedFilters>(() =>
    buildFilters(initialValues)
  );
  const [refreshKey, setRefreshKey] = useState(0);

  const fetchRows = useCallback(
    async (query: BasicTableQuery): Promise<PaginationType<AnyObject>> => {
      const response = await axiosInstance.post<PaginationType<AnyObject>>(
        'ActivityLog/search',
        buildRequest(filters, query)
      );
      return response.data;
    },
    [filters]
  );

  const applyFilters = (values: FormValues) => {
    setFilters(buildFilters(values));
    setRefreshKey((current) => current + 1);
  };

  const resetFilters = () => {
    form.setFieldsValue(initialValues);
    setFilters(buildFilters(initialValues));
    setRefreshKey((current) => current + 1);
  };

  if (!isAdmin) {
    return (
      <>
        <PageHeader title="Activity Log" />
        <Alert
          type="warning"
          showIcon
          message="Admins only"
          description="You don't have permission to view the activity log."
        />
      </>
    );
  }

  return (
    <>
      <PageHeader title="Activity Log" />

      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={initialValues}
          onFinish={applyFilters}
        >
          <Row gutter={[16, 16]} align="bottom">
            <Col xs={24} md={12} lg={8}>
              <Form.Item label="From / To" name="dateRange">
                <DatePicker.RangePicker
                  allowClear={false}
                  style={{ width: '100%' }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={6}>
              <Form.Item label="User" name="userId">
                <Input allowClear placeholder="User id / name" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={6}>
              <Form.Item label="Event type" name="eventType">
                <Select options={eventTypeOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} lg={4}>
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

      <BasicTable<AnyObject>
        title="Activity Log"
        tableId="ActivityLogTable"
        columns={columns}
        fetchData={fetchRows}
        showActions={false}
        enabled
        refreshKey={refreshKey}
        initialSortColumn="timestampUtc"
        initialSortOrder="desc"
        lazyTotalCount
        showRowNumber
        rowNumberTitle="No"
        emptyText="No activity for the selected filters."
      />
    </>
  );
};

export default ActivityLogList;
