import { useCallback, useMemo, useState } from 'react';
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
  ReportFilterConfig,
  ReportPageConfig,
} from '../config/reportTypes';

type FilterValue =
  | string
  | number
  | boolean
  | Dayjs
  | [Dayjs, Dayjs]
  | undefined;
type FilterFormValues = Record<string, FilterValue>;

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

const toApiDate = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).format(
    'YYYY-MM-DDTHH:mm:ss'
  );

const getInitialFilterValue = (filter: ReportFilterConfig): FilterValue => {
  if (filter.type === 'dateRange') {
    const today = dayjs();
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

const normalizeFilters = (
  filters: ReportFilterConfig[],
  values: FilterFormValues
) =>
  filters.reduce<Record<string, unknown>>((request, filter) => {
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
  if (column.dataType === 'date') {
    return { ...column, render: formatDate };
  }

  if (column.dataType === 'boolean') {
    return { ...column, render: formatBoolean };
  }

  return column;
};

const renderFilter = (filter: ReportFilterConfig) => {
  if (filter.type === 'dateRange') {
    return <DatePicker.RangePicker allowClear={false} style={{ width: '100%' }} />;
  }

  if (filter.type === 'date') {
    return <DatePicker allowClear={false} style={{ width: '100%' }} />;
  }

  if (filter.type === 'number') {
    return <InputNumber min={0} style={{ width: '100%' }} />;
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

  return <Input />;
};

interface GenericReportPageProps {
  config: ReportPageConfig;
}

const GenericReportPage = ({ config }: GenericReportPageProps) => {
  const [form] = Form.useForm<FilterFormValues>();
  const tableColumns = useMemo(
    () => config.columns.map(toTableColumn),
    [config.columns]
  );
  const initialFormValues = useMemo(
    () => buildInitialValues(config.filters),
    [config.filters]
  );
  const [filters, setFilters] = useState<Record<string, unknown>>(() =>
    normalizeFilters(config.filters, initialFormValues)
  );
  const [refreshKey, setRefreshKey] = useState(0);

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
      const response = await axiosInstance.post(
        config.excelRoute,
        buildRequest(filters, query),
        { responseType: 'blob' }
      );
      const blob = new Blob([response.data], {
        type: String(response.headers['content-type'] ?? excelContentType),
      });

      downloadBlob(blob, config.excelFileName);
    },
    [config.excelFileName, config.excelRoute, filters]
  );

  const applyFilters = (values: FilterFormValues) => {
    setFilters(normalizeFilters(config.filters, values));
    setRefreshKey((current) => current + 1);
  };

  const resetFilters = () => {
    form.setFieldsValue(initialFormValues);
    setFilters(normalizeFilters(config.filters, initialFormValues));
    setRefreshKey((current) => current + 1);
  };

  return (
    <>
      <PageHeader title={config.title} />

      {config.filters.length > 0 && (
        <Card>
          <Form
            form={form}
            layout="vertical"
            initialValues={initialFormValues}
            onFinish={applyFilters}
          >
            <Row gutter={[16, 16]} align="bottom">
              {config.filters.map((filter) => (
                <Col xs={24} md={12} lg={6} key={filter.name}>
                  <Form.Item
                    label={filter.label}
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
                    {renderFilter(filter)}
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
        tableId={`${config.controllerName}Table`}
        columns={tableColumns}
        fetchData={fetchRows}
        onExcel={generateExcel}
        showActions={false}
        refreshKey={refreshKey}
        initialSortColumn={config.initialSortColumn}
        initialSortOrder="desc"
        excelFileName={config.excelFileName}
      />
    </>
  );
};

export default GenericReportPage;
