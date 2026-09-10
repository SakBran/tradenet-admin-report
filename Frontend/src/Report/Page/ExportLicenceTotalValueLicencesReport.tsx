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

const config = reportConfigs.ExportLicenceTotalValueLicencesReport;

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

interface LicenceRow {
  paThaKaType: string;
  noOfLicences: number;
}

interface TotalValueLicencesSummary {
  totalValueByCurrency: ValueRow[];
  totalLicencesByPaThaKaType: LicenceRow[];
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

const ExportLicenceTotalValueLicencesReport = () => {
  const [form] = Form.useForm<FormValues>();
  const [paThaKaTypes, setPaThaKaTypes] = useState<LookupOption[]>([]);
  const [sections, setSections] = useState<LookupOption[]>([]);
  const [summary, setSummary] = useState<TotalValueLicencesSummary | null>(null);
  const [appliedRange, setAppliedRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [loading, setLoading] = useState(false);

  const initialValues = useMemo<FormValues>(
    () => ({
      dateRange: [dayjs().subtract(3, 'month').startOf('day'), dayjs()],
      PaThaKaTypeId: 0,
      ExportImportSectionId: 0,
    }),
    []
  );

  useEffect(() => {
    let mounted = true;
    Promise.all([
      axiosInstance.get<LookupOption[]>('ReportLookups/paThaKaTypes'),
      axiosInstance.get<LookupOption[]>('ReportLookups/exportLicenceSections'),
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
      const response = await axiosInstance.post<TotalValueLicencesSummary>(
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

    // The shared funnel, not a hand-rolled post: it reports a rejected request instead of
    // swallowing it (this page used to await an un-caught POST, so a failure made the
    // button do nothing at all), and it follows the queued job to completion so the file
    // downloads here. No presentation spec -- this controller is an
    // IExcelReportLayoutProvider and builds the two-section sheet itself.
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
    ? `Export Licences Total Value & Licences (${appliedRange[0].format(
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

  const licenceColumns: TableProps<LicenceRow>['columns'] = [
    {
      title: 'Sr.No.',
      key: 'sr',
      width: 90,
      render: (_value, _row, index) => index + 1,
    },
    {
      title: 'Total Licences',
      dataIndex: 'noOfLicences',
      key: 'noOfLicences',
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
              <Form.Item label="Export Section" name="ExportImportSectionId">
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
            Total Licences
          </Typography.Title>
          <Table<LicenceRow>
            size="small"
            rowKey="paThaKaType"
            columns={licenceColumns}
            dataSource={summary.totalLicencesByPaThaKaType}
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

export default ExportLicenceTotalValueLicencesReport;
