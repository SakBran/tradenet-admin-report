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

const config = reportConfigs.BorderImportLicenceTotalValueLicencesReport;

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

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

interface ExcelEnqueueResult {
  status: 'Ready' | 'Queued' | 'Processing';
  downloadUrl?: string;
  fileName?: string;
}

type FormValues = {
  dateRange: [Dayjs, Dayjs];
  SakhanId: number;
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
    Type: 'Border',
    FromDate: toApiDate(from, 'start'),
    ToDate: toApiDate(to, 'end'),
    SakhanId: values.SakhanId ?? 0,
    PaThaKaTypeId: values.PaThaKaTypeId ?? 0,
    ExportImportSectionId: values.ExportImportSectionId ?? 0,
  };
};

const BorderImportLicenceTotalValueLicencesReport = () => {
  const [form] = Form.useForm<FormValues>();
  const [sakhans, setSakhans] = useState<LookupOption[]>([]);
  const [paThaKaTypes, setPaThaKaTypes] = useState<LookupOption[]>([]);
  const [sections, setSections] = useState<LookupOption[]>([]);
  const [summary, setSummary] = useState<TotalValueLicencesSummary | null>(null);
  const [appliedRange, setAppliedRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [loading, setLoading] = useState(false);

  const initialValues = useMemo<FormValues>(
    () => ({
      dateRange: [dayjs().startOf('month'), dayjs()],
      SakhanId: 0,
      PaThaKaTypeId: 0,
      ExportImportSectionId: 0,
    }),
    []
  );

  useEffect(() => {
    let mounted = true;
    Promise.all([
      axiosInstance.get<LookupOption[]>('ReportLookups/sakhans'),
      axiosInstance.get<LookupOption[]>('ReportLookups/paThaKaTypes'),
      axiosInstance.get<LookupOption[]>('ReportLookups/borderImportLicenceSections'),
    ])
      .then(([sakhansResponse, typesResponse, sectionsResponse]) => {
        if (!mounted) {
          return;
        }
        setSakhans(sakhansResponse.data);
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

    const response = await axiosInstance.post<ExcelEnqueueResult>(
      config.excelRoute,
      buildRequest(values)
    );
    const result = response.data;

    if (result.status === 'Ready' && result.downloadUrl) {
      const fileResponse = await axiosInstance.get(result.downloadUrl, {
        responseType: 'blob',
      });
      const blob = new Blob([fileResponse.data], {
        type: String(fileResponse.headers['content-type'] ?? excelContentType),
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = result.fileName ?? config.excelFileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
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
  };

  const heading = appliedRange
    ? `Border Import Licences Total Value & Licences (${appliedRange[0].format(
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
              <Form.Item label="Sakhan" name="SakhanId">
                <Select
                  showSearch
                  optionFilterProp="label"
                  options={toSelectOptions(sakhans)}
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
            <Col xs={24}>
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

export default BorderImportLicenceTotalValueLicencesReport;
