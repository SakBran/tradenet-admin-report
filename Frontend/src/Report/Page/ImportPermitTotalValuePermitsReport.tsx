import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Row,
  Select,
  Space,
  Table,
  TableProps,
  Typography,
  message,
} from 'antd';
import {
  FileExcelOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import dayjs, { Dayjs } from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { PageHeader } from '../../components';
import { reportConfigs } from '../config/reportConfigs';
import {
  enqueueExcelExport,
  ExcelSpecRejectedError,
} from '../excel/excelEnqueue';

// This report mirrors the requested Import Permit total-value report, which
// stacks TWO summary tables (Total Value per Currency, Total Permits per Pa Tha Ka
// Type) plus a single "Total USD Value" box. The generic single-grid page can't render
// that shape, so this report has its own page that calls the composite endpoint.
const config = reportConfigs.ImportPermitTotalValuePermitsReport;

interface LookupOption {
  id: number;
  code: string;
  label: string;
  value?: string | number;
}

interface ValueRow {
  currency: string;
  totalValue: number;
}

interface PermitRow {
  paThaKaType: string;
  noOfPermits: number;
}

interface TotalValuePermitsSummary {
  totalValueByCurrency: ValueRow[];
  totalPermitsByPaThaKaType: PermitRow[];
  totalUsdValue: number;
}

type FormValues = {
  dateRange: [Dayjs, Dayjs];
  PaThaKaTypeId: number;
  ExportImportSectionId: number;
};

const formatValue = (value: number) =>
  Number.isFinite(value)
    ? value.toLocaleString('en-US', {
        minimumFractionDigits: 4,
        maximumFractionDigits: 4,
      })
    : '0.0000';

const toApiDate = (value: Dayjs, edge: 'start' | 'end') =>
  (edge === 'start' ? value.startOf('day') : value.endOf('day')).format(
    'YYYY-MM-DDTHH:mm:ss'
  );

const toSelectOptions = (options: LookupOption[] = []) => [
  { label: 'All', value: 0 },
  ...options.map((option) => ({
    label: option.code ? `${option.label} (${option.code})` : option.label,
    value: option.value ?? option.id,
  })),
];

const buildRequest = (values: FormValues) => {
  const [from, to] = values.dateRange;
  return {
    Type: 'Oversea',
    FromDate: toApiDate(from, 'start'),
    ToDate: toApiDate(to, 'end'),
    PaThaKaTypeId: values.PaThaKaTypeId ?? 0,
    ExportImportSectionId: values.ExportImportSectionId ?? 0,
  };
};

const ImportPermitTotalValuePermitsReport = () => {
  const [form] = Form.useForm<FormValues>();
  const [paThaKaTypes, setPaThaKaTypes] = useState<LookupOption[]>([]);
  const [sections, setSections] = useState<LookupOption[]>([]);
  const [summary, setSummary] = useState<TotalValuePermitsSummary | null>(null);
  const [appliedRange, setAppliedRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [loading, setLoading] = useState(false);

  const initialValues = useMemo<FormValues>(
    () => ({
      dateRange: [dayjs().startOf('month'), dayjs()],
      PaThaKaTypeId: 0,
      ExportImportSectionId: 0,
    }),
    []
  );

  useEffect(() => {
    let mounted = true;
    Promise.all([
      axiosInstance.get<LookupOption[]>('ReportLookups/paThaKaTypes'),
      axiosInstance.get<LookupOption[]>('ReportLookups/importPermitSections'),
    ])
      .then(([typesResponse, sectionsResponse]) => {
        if (!mounted) {
          return;
        }
        setPaThaKaTypes(typesResponse.data);
        setSections(sectionsResponse.data);
      })
      .catch(() => {
        /* lookups are optional filters; ignore load failures */
      });
    return () => {
      mounted = false;
    };
  }, []);

  const loadSummary = useCallback(async (values: FormValues) => {
    setLoading(true);
    try {
      const response = await axiosInstance.post<TotalValuePermitsSummary>(
        config.apiRoute,
        buildRequest(values)
      );
      setSummary(response.data);
      setAppliedRange(values.dateRange);
    } catch {
      message.error('Could not load the report. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  const resetFilters = () => {
    form.setFieldsValue(initialValues);
    setSummary(null);
    setAppliedRange(null);
  };

  const generateExcel = async () => {
    let values: FormValues;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }

    // The shared export flow follows the queued job to completion. The backend controller
    // owns the two-section worksheet layout, so this page does not send a presentation spec.
    try {
      await enqueueExcelExport(
        config.excelRoute,
        buildRequest(values),
        undefined,
        config.excelFileName
      );
    } catch (error) {
      if (!(error instanceof ExcelSpecRejectedError)) {
        message.error('Excel export failed. Please try again.');
      }
    }
  };

  const heading = appliedRange
    ? `Import Permits Total Value & Permits (${appliedRange[0].format(
        'DD/MM/YYYY'
      )}) To (${appliedRange[1].format('DD/MM/YYYY')})`
    : null;

  const valueColumns: TableProps<ValueRow>['columns'] = [
    {
      title: 'Sr.No.',
      key: 'sr',
      width: 90,
      render: (_value, _row, index) => index + 1,
    },
    {
      title: 'Total Value',
      dataIndex: 'totalValue',
      key: 'totalValue',
      align: 'right',
      render: (value: number) => formatValue(value),
    },
    {
      title: 'Currency',
      dataIndex: 'currency',
      key: 'currency',
    },
  ];

  const permitColumns: TableProps<PermitRow>['columns'] = [
    {
      title: 'Sr.No.',
      key: 'sr',
      width: 90,
      render: (_value, _row, index) => index + 1,
    },
    {
      title: 'Total Permits',
      dataIndex: 'noOfPermits',
      key: 'noOfPermits',
      align: 'right',
    },
    {
      title: 'Pa Tha Ka Type',
      dataIndex: 'paThaKaType',
      key: 'paThaKaType',
    },
  ];

  return (
    <>
      <PageHeader title={config.title} />

      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={initialValues}
          onFinish={loadSummary}
        >
          <Row gutter={[16, 16]} align="bottom">
            <Col xs={24} md={12} lg={6}>
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
            <Col xs={24} md={12} lg={6}>
              <Form.Item label="EIR Card Type" name="PaThaKaTypeId">
                <Select
                  showSearch
                  optionFilterProp="label"
                  options={toSelectOptions(paThaKaTypes)}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={6}>
              <Form.Item label="Import Section" name="ExportImportSectionId">
                <Select
                  showSearch
                  optionFilterProp="label"
                  options={toSelectOptions(sections)}
                />
              </Form.Item>
            </Col>
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
                  <Button onClick={generateExcel} icon={<FileExcelOutlined />}>
                    Excel
                  </Button>
                </Space>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {summary && (
        <Card style={{ marginTop: 16 }} loading={loading}>
          {heading && (
            <Typography.Title
              level={5}
              style={{ textAlign: 'center', marginBottom: 24 }}
            >
              {heading}
            </Typography.Title>
          )}

          <Typography.Title level={5}>Total Value</Typography.Title>
          <Table<ValueRow>
            size="small"
            rowKey="currency"
            columns={valueColumns}
            dataSource={summary.totalValueByCurrency}
            pagination={false}
            bordered
          />

          <Typography.Title level={5} style={{ marginTop: 24 }}>
            Total Permits
          </Typography.Title>
          <Table<PermitRow>
            size="small"
            rowKey="paThaKaType"
            columns={permitColumns}
            dataSource={summary.totalPermitsByPaThaKaType}
            pagination={false}
            bordered
          />

          <div
            style={{
              marginTop: 24,
              display: 'flex',
              justifyContent: 'flex-end',
            }}
          >
            <Typography.Text strong style={{ fontSize: 16 }}>
              {`Total USD Value: ${formatValue(summary.totalUsdValue)}`}
            </Typography.Text>
          </div>
        </Card>
      )}
    </>
  );
};

export default ImportPermitTotalValuePermitsReport;
