import { Fragment, useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Flex,
  Form,
  Pagination,
  Row,
  Select,
  Space,
  Tag,
  Typography,
  message,
} from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs, { Dayjs } from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import '../../components/My Components/Table/style.css';

const { RangePicker } = DatePicker;
const { Text } = Typography;

type DateRange = [Dayjs, Dayjs];
type SummaryPeriod = 'Daily' | 'Monthly' | 'Yearly';

type FilterFormValues = {
  dateRange: DateRange;
  period: SummaryPeriod;
};

type TemplateSummaryRow = {
  date: string;
  importLicenceCount: number;
  importLicenceAmount: number;
  borderImportLicenceCount: number;
  borderImportLicenceAmount: number;
  exportLicenceCount: number;
  exportLicenceAmount: number;
  borderExportLicenceCount: number;
  borderExportLicenceAmount: number;
  importPermitCount: number;
  importPermitAmount: number;
  borderImportPermitCount: number;
  borderImportPermitAmount: number;
  exportPermitCount: number;
  exportPermitAmount: number;
  borderExportPermitCount: number;
  borderExportPermitAmount: number;
};

type TemplateSummaryResult = {
  startDate: string;
  endDate: string;
  period: SummaryPeriod;
  rows: TemplateSummaryRow[];
};

const numberFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 4,
});

const formatNumber = (value: number | null | undefined) =>
  numberFormatter.format(value ?? 0);

const initialDateRange: DateRange = [
  dayjs('2020-01-01'),
  dayjs('2025-12-31'),
];

const initialPeriod: SummaryPeriod = 'Yearly';

const periodOptions: { label: string; value: SummaryPeriod }[] = [
  { label: 'Daily', value: 'Daily' },
  { label: 'Monthly', value: 'Monthly' },
  { label: 'Yearly', value: 'Yearly' },
];

const getRowTotalCount = (row: TemplateSummaryRow) =>
  row.importLicenceCount +
  row.borderImportLicenceCount +
  row.exportLicenceCount +
  row.borderExportLicenceCount +
  row.importPermitCount +
  row.borderImportPermitCount +
  row.exportPermitCount +
  row.borderExportPermitCount;

const getRowTotalAmount = (row: TemplateSummaryRow) =>
  row.importLicenceAmount +
  row.borderImportLicenceAmount +
  row.exportLicenceAmount +
  row.borderExportLicenceAmount +
  row.importPermitAmount +
  row.borderImportPermitAmount +
  row.exportPermitAmount +
  row.borderExportPermitAmount;

const getPeriodLabel = (selectedPeriod: SummaryPeriod) =>
  selectedPeriod === 'Yearly'
    ? 'နှစ်အလိုက်'
    : selectedPeriod === 'Monthly'
    ? 'လအလိုက်'
    : 'နေ့စဉ်';

const getDateColumnTitle = (selectedPeriod: SummaryPeriod) =>
  selectedPeriod === 'Yearly'
    ? 'နှစ်'
    : selectedPeriod === 'Monthly'
    ? 'လ'
    : 'နေ့စွဲ';

const formatPeriodDate = (value: string, selectedPeriod: SummaryPeriod) => {
  const date = dayjs(value);
  if (selectedPeriod === 'Yearly') {
    return date.format('YYYY');
  }

  if (selectedPeriod === 'Monthly') {
    return date.format('YYYY-MM');
  }

  return date.format('YYYY-MM-DD');
};

export const HowToUsePage = () => {
  const [form] = Form.useForm<FilterFormValues>();
  const [dateRange, setDateRange] = useState<DateRange>(initialDateRange);
  const [period, setPeriod] = useState<SummaryPeriod>(initialPeriod);
  const [rows, setRows] = useState<TemplateSummaryRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(20);

  const loadSummary = useCallback(async (
    range: DateRange,
    selectedPeriod: SummaryPeriod
  ) => {
    setLoading(true);
    try {
      const response = await axiosInstance.get<TemplateSummaryResult>(
        'DataImport/Summary',
        {
          params: {
            startDate: range[0].format('YYYY-MM-DD'),
            endDate: range[1].format('YYYY-MM-DD'),
            period: selectedPeriod,
          },
        }
      );
      setRows(response.data.rows ?? []);
      setPageIndex(0);
    } catch {
      message.error('Could not load template summary data.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSummary(dateRange, period);
  }, [dateRange, loadSummary, period]);

  const applyFilters = (values: FilterFormValues) => {
    setDateRange(values.dateRange);
    setPeriod(values.period);
  };

  const resetFilters = () => {
    form.setFieldsValue({
      dateRange: initialDateRange,
      period: initialPeriod,
    });
    setDateRange(initialDateRange);
    setPeriod(initialPeriod);
  };

  const totals = useMemo(
    () =>
      rows.reduce(
        (total, row) => ({
          count: total.count + getRowTotalCount(row),
          amount: total.amount + getRowTotalAmount(row),
        }),
        { count: 0, amount: 0 }
      ),
    [rows]
  );

  const pageRows = useMemo(
    () => rows.slice(pageIndex * pageSize, pageIndex * pageSize + pageSize),
    [pageIndex, pageSize, rows]
  );

  const pageTotals = useMemo(
    () =>
      pageRows.reduce(
        (total, row) => ({
          count: total.count + getRowTotalCount(row),
          amount: total.amount + getRowTotalAmount(row),
        }),
        { count: 0, amount: 0 }
      ),
    [pageRows]
  );

  return (
    <Flex vertical gap={16}>
      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={{ dateRange: initialDateRange, period: initialPeriod }}
          onFinish={applyFilters}
        >
          <Row gutter={[16, 16]} align="bottom">
            <Col xs={24} md={12} lg={6}>
              <Form.Item
                label="From Date / To Date"
                name="dateRange"
                rules={[{ required: true, message: 'Date range is required' }]}
              >
                <RangePicker
                  allowClear={false}
                  format="YYYY-MM-DD"
                  inputReadOnly
                  style={{ width: '100%' }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={4}>
              <Form.Item
                label="Option"
                name="period"
                rules={[{ required: true, message: 'Option is required' }]}
              >
                <Select options={periodOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={6}>
              <Form.Item>
                <Space wrap>
                  <Button
                    type="primary"
                    htmlType="submit"
                    icon={<SearchOutlined />}
                    loading={loading}
                  >
                    Filter
                  </Button>
                  <Button onClick={resetFilters} icon={<ReloadOutlined />}>
                    Reset
                  </Button>
                </Space>
              </Form.Item>
            </Col>
            <Col xs={24} lg={8}>
              <Form.Item>
                <Flex gap={8} justify="flex-end" wrap="wrap">
                  <Tag color="blue">{`စုစုပေါင်း စောင်ရေ ${formatNumber(totals.count)}`}</Tag>
                  <Tag color="green">{`စုစုပေါင်း တန်ဖိုး ${formatNumber(totals.amount)}`}</Tag>
                </Flex>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      <div className="report-viewer-container">
        <div className="report-viewer-toolbar">
          <Text strong>{`${getPeriodLabel(period)} TemplateDB စာရင်းချုပ်`}</Text>
        </div>
        <div className="table-container">
          {loading && (
            <Flex
              className="table-loading-banner"
              align="center"
              justify="space-between"
              gap="middle"
              wrap="wrap"
            >
              <Flex vertical gap={4}>
                <Text strong>စာရင်းများ ရယူနေပါသည်</Text>
                <Text type="secondary">TemplateDB မှ အချက်အလက်များကို ပြင်ဆင်နေပါသည်...</Text>
              </Flex>
            </Flex>
          )}

          <table id="templateSummaryTable">
            <thead>
              <tr className="report-header-row">
                <th colSpan={18}>{`${getPeriodLabel(period)} Template စာရင်းချုပ်`}</th>
              </tr>
              <tr>
                <th rowSpan={3}>စဉ်</th>
                <th rowSpan={3}>{getDateColumnTitle(period)}</th>
                <th colSpan={4}>ပို့ကုန် လိုင်စင်</th>
                <th colSpan={4}>သွင်းကုန် လိုင်စင်</th>
                <th colSpan={4}>ပို့ကုန် (ပါမစ်)</th>
                <th colSpan={4}>သွင်းကုန် (ပါမစ်)</th>
              </tr>
              <tr>
                <th colSpan={2}>ပင်လယ်ရေကြောင်း</th>
                <th colSpan={2}>နယ်စပ်</th>
                <th colSpan={2}>ပင်လယ်ရေကြောင်း</th>
                <th colSpan={2}>နယ်စပ်</th>
                <th colSpan={2}>ပင်လယ်ရေကြောင်း</th>
                <th colSpan={2}>နယ်စပ်</th>
                <th colSpan={2}>ပင်လယ်ရေကြောင်း</th>
                <th colSpan={2}>နယ်စပ်</th>
              </tr>
              <tr>
                {Array.from({ length: 8 }).map((_, index) => (
                  <Fragment key={index}>
                    <th>စောင်ရေ</th>
                    <th>တန်ဖိုး</th>
                  </Fragment>
                ))}
              </tr>
            </thead>
            <tbody>
              {pageRows.length ? (
                pageRows.map((row, index) => (
                  <tr key={row.date}>
                    <td>{pageIndex * pageSize + index + 1}</td>
                    <td>{formatPeriodDate(row.date, period)}</td>
                    <td className="col-numeric">{formatNumber(row.exportLicenceCount)}</td>
                    <td className="col-numeric">{formatNumber(row.exportLicenceAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderExportLicenceCount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderExportLicenceAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.importLicenceCount)}</td>
                    <td className="col-numeric">{formatNumber(row.importLicenceAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderImportLicenceCount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderImportLicenceAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.exportPermitCount)}</td>
                    <td className="col-numeric">{formatNumber(row.exportPermitAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderExportPermitCount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderExportPermitAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.importPermitCount)}</td>
                    <td className="col-numeric">{formatNumber(row.importPermitAmount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderImportPermitCount)}</td>
                    <td className="col-numeric">{formatNumber(row.borderImportPermitAmount)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={18} style={{ textAlign: 'center' }}>
                    {loading ? 'စာရင်းများ ရယူနေပါသည်' : 'စာရင်း မရှိပါ'}
                  </td>
                </tr>
              )}
            </tbody>
            <tfoot>
              <tr className="report-total-row">
                <td colSpan={2}>စုစုပေါင်း</td>
                <td colSpan={8} className="col-numeric">{`စောင်ရေ ${formatNumber(pageTotals.count)}`}</td>
                <td colSpan={8} className="col-numeric">{`တန်ဖိုး ${formatNumber(pageTotals.amount)}`}</td>
              </tr>
            </tfoot>
          </table>
        </div>
        <div className="pagination">
          <Pagination
            showSizeChanger
            current={pageIndex + 1}
            pageSize={pageSize}
            total={rows.length}
            pageSizeOptions={[10, 20, 50, 100]}
            showTotal={(total, range) =>
              `${range[0]}-${range[1]} / စုစုပေါင်း ${total}`
            }
            onChange={(page, size) => {
              setPageIndex(page - 1);
              setPageSize(size);
            }}
          />
        </div>
      </div>
    </Flex>
  );
};

export default HowToUsePage;
