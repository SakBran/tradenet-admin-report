import { CSSProperties, useEffect, useState } from 'react';
import {
  Button,
  Card,
  DatePicker,
  Form,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  CheckCircleFilled,
  CloseCircleFilled,
  ReloadOutlined,
  SaveOutlined,
} from '@ant-design/icons';
import dayjs, { Dayjs } from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { PageHeader } from '../../components';
import { reportConfigs } from '../config/reportConfigs';

const { RangePicker } = DatePicker;
const config = reportConfigs.DataImport;

const calendarCellStyle: CSSProperties = {
  minHeight: 30,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 4,
  borderRadius: 4,
};

type FormValues = {
  licenceType: string;
  dateRange: [Dayjs, Dayjs];
};

type SaveResult = {
  licenceType: string;
  startDate: string;
  endDate: string;
  rows: SaveResultRow[];
};

type SaveResultRow = {
  id: number;
  licenceType: string;
  licenceTypeLabel: string;
  totalCount: number;
  totalAmount: number;
  licenceDate: string;
  createdDate: string;
};

type CalendarStatusResult = {
  year: number;
  startDate: string;
  endDate: string;
  days: CalendarDayStatus[];
};

type CalendarDayStatus = {
  date: string;
  isComplete: boolean;
  importedTypeCount: number;
  requiredTypeCount: number;
};

const licenceTypeOptions = [
  { label: 'All', value: 'All' },
  { label: 'Import Licence', value: 'ImportLicence' },
  { label: 'Export Licence', value: 'ExportLicence' },
  { label: 'Border Import Licence', value: 'BorderImportLicence' },
  { label: 'Border Export Licence', value: 'BorderExportLicence' },
  { label: 'Import Permit', value: 'ImportPermit' },
  { label: 'Export Permit', value: 'ExportPermit' },
  { label: 'Border Import Permit', value: 'BorderImportPermit' },
  { label: 'Border Export Permit', value: 'BorderExportPermit' },
];

const ImportLicenceDataImport = () => {
  const [form] = Form.useForm<FormValues>();
  const [saving, setSaving] = useState(false);
  const [result, setResult] = useState<SaveResult | null>(null);
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [statusYear, setStatusYear] = useState(dayjs().year());
  const [calendarStatus, setCalendarStatus] =
    useState<CalendarStatusResult | null>(null);

  const rows = result?.rows ?? [];
  const totalCount = rows.reduce((sum, row) => sum + row.totalCount, 0);
  const totalAmount = rows.reduce(
    (sum, row) => sum + Number(row.totalAmount),
    0
  );
  const completeDays =
    calendarStatus?.days.filter((day) => day.isComplete).length ?? 0;
  const missingDays =
    calendarStatus?.days.filter((day) => !day.isComplete).length ?? 0;
  const calendarStatusMap = new Map(
    (calendarStatus?.days ?? []).map((day) => [
      dayjs(day.date).format('YYYY-MM-DD'),
      day,
    ])
  );
  const yearOptions = Array.from(
    { length: dayjs().year() - 2021 + 1 },
    (_, index) => {
      const year = 2021 + index;
      return { label: String(year), value: year };
    }
  ).reverse();

  const loadScheduleStatus = async (year = statusYear) => {
    setCheckingStatus(true);
    try {
      const response = await axiosInstance.get<CalendarStatusResult>(
        `${config.apiRoute}/CalendarStatus`,
        { params: { year } }
      );
      setCalendarStatus(response.data);
    } catch {
      message.error('Could not load schedule checklist.');
    } finally {
      setCheckingStatus(false);
    }
  };

  const handleSave = async (values: FormValues) => {
    setSaving(true);
    try {
      const response = await axiosInstance.post<SaveResult>(config.apiRoute, {
        licenceType: values.licenceType,
        startDate: values.dateRange[0]
          .startOf('day')
          .format('YYYY-MM-DDTHH:mm:ss'),
        endDate: values.dateRange[1]
          .startOf('day')
          .format('YYYY-MM-DDTHH:mm:ss'),
      });
      setResult(response.data);
      message.success('Daily data saved to TemplateDB.');
    } catch {
      message.error('Could not save the data import.');
    } finally {
      setSaving(false);
    }
  };

  useEffect(() => {
    loadScheduleStatus(statusYear);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const renderMonth = (monthIndex: number) => {
    const month = dayjs(
      `${statusYear}-${String(monthIndex + 1).padStart(2, '0')}-01`
    );
    const cells = [
      ...Array.from({ length: month.day() }, () => null),
      ...Array.from({ length: month.daysInMonth() }, (_, index) =>
        month.date(index + 1)
      ),
    ];

    return (
      <div
        key={monthIndex}
        style={{
          border: '1px solid #f0f0f0',
          borderRadius: 6,
          minWidth: 0,
          padding: 12,
        }}
      >
        <Typography.Text strong style={{ display: 'block', marginBottom: 12 }}>
          {month.format('MMMM')}
        </Typography.Text>
        <div
          style={{
            display: 'grid',
            gap: 6,
            gridTemplateColumns: 'repeat(7, minmax(0, 1fr))',
          }}
        >
          {['S', 'M', 'T', 'W', 'T', 'F', 'S'].map((day, index) => (
            <Typography.Text
              key={`${day}-${index}`}
              type="secondary"
              style={{ ...calendarCellStyle, minHeight: 22 }}
            >
              {day}
            </Typography.Text>
          ))}
          {cells.map((date, index) => {
            if (!date) {
              return <span key={`blank-${index}`} />;
            }

            const dateKey = date.format('YYYY-MM-DD');
            const status = calendarStatusMap.get(dateKey);
            const isFutureDate =
              !!calendarStatus &&
              date.isAfter(dayjs(calendarStatus.endDate), 'day');

            return (
              <div
                key={dateKey}
                title={
                  isFutureDate
                    ? `${dateKey}: Future date`
                    : status
                    ? `${dateKey}: ${status.isComplete ? 'Complete' : 'Missing'}`
                    : `${dateKey}: Not checked`
                }
                style={{
                  ...calendarCellStyle,
                  background: isFutureDate
                    ? '#fafafa'
                    : status?.isComplete
                    ? '#f6ffed'
                    : '#fff1f0',
                }}
              >
                <Typography.Text style={{ fontSize: 12, lineHeight: 1 }}>
                  {date.date()}
                </Typography.Text>
                {isFutureDate ? null : status?.isComplete ? (
                  <CheckCircleFilled
                    style={{ color: '#52c41a', fontSize: 13 }}
                  />
                ) : (
                  <CloseCircleFilled
                    style={{ color: '#ff4d4f', fontSize: 13 }}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
    );
  };

  return (
    <>
      <PageHeader title={config.title} />
      <Card style={{ marginBottom: 16 }}>
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <div
            style={{
              alignItems: 'flex-end',
              display: 'flex',
              flexWrap: 'wrap',
              gap: 16,
              justifyContent: 'space-between',
            }}
          >
            <Typography.Text strong style={{ fontSize: 18, lineHeight: '32px' }}>
              Schedule Checklist
            </Typography.Text>
            <Space align="end" wrap>
              <Space direction="vertical" size={4}>
                <Typography.Text type="secondary">Year</Typography.Text>
                <Select
                  value={statusYear}
                  options={yearOptions}
                  style={{ width: 180 }}
                  onChange={(value) => {
                    setStatusYear(value);
                    loadScheduleStatus(value);
                  }}
                />
              </Space>
              <Button
                icon={<ReloadOutlined />}
                loading={checkingStatus}
                onClick={() => loadScheduleStatus()}
              >
                Check Status
              </Button>
              {calendarStatus && (
                <Space size={8} style={{ alignItems: 'center', height: 32 }}>
                  <Tag color="success" style={{ marginInlineEnd: 0 }}>
                    {`OK ${completeDays}`}
                  </Tag>
                  <Tag color="error" style={{ marginInlineEnd: 0 }}>
                    {`Missing ${missingDays}`}
                  </Tag>
                </Space>
              )}
            </Space>
          </div>

          {calendarStatus && (
            <div
              style={{
                display: 'grid',
                gap: 16,
                gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
              }}
            >
              {Array.from({ length: 12 }, (_, index) => renderMonth(index))}
            </div>
          )}
        </Space>
      </Card>

      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            licenceType: 'All',
            dateRange: [dayjs('2025-05-01'), dayjs('2025-05-01')],
          }}
          onFinish={handleSave}
        >
          <Form.Item
            label="Licence Type"
            name="licenceType"
            rules={[{ required: true, message: 'Licence type is required' }]}
          >
            <Select style={{ width: 280 }} options={licenceTypeOptions} />
          </Form.Item>
          <Form.Item
            label="Date Range"
            name="dateRange"
            rules={[{ required: true, message: 'Date range is required' }]}
          >
            <RangePicker allowClear={false} style={{ width: 280 }} />
          </Form.Item>
          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              icon={<SaveOutlined />}
              loading={saving}
            >
              Save Data
            </Button>
          </Form.Item>
        </Form>
      </Card>

      {result && (
        <Card style={{ marginTop: 16 }}>
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Space direction="vertical" size={4}>
              <Typography.Text strong>{`Saved Rows: ${rows.length}`}</Typography.Text>
              <Typography.Text>{`Date Range: ${dayjs(result.startDate).format('YYYY-MM-DD')} to ${dayjs(result.endDate).format('YYYY-MM-DD')}`}</Typography.Text>
              <Typography.Text>{`Total Count: ${totalCount}`}</Typography.Text>
              <Typography.Text>{`Total Amount (USD): ${totalAmount.toLocaleString('en-US', {
                minimumFractionDigits: 4,
                maximumFractionDigits: 4,
              })}`}</Typography.Text>
            </Space>
            <Table<SaveResultRow>
              rowKey={(row) => `${row.licenceType}-${row.licenceDate}`}
              size="small"
              pagination={{ pageSize: 20 }}
              dataSource={rows}
              columns={[
                {
                  key: 'licenceType',
                  dataIndex: 'licenceTypeLabel',
                  title: 'Licence Type',
                },
                {
                  key: 'licenceDate',
                  dataIndex: 'licenceDate',
                  title: 'Date',
                  render: (value: string) => dayjs(value).format('YYYY-MM-DD'),
                },
                {
                  key: 'totalCount',
                  dataIndex: 'totalCount',
                  title: 'Total Count',
                  align: 'right',
                },
                {
                  key: 'totalAmount',
                  dataIndex: 'totalAmount',
                  title: 'Total Amount (USD)',
                  align: 'right',
                  render: (value: number) =>
                    Number(value).toLocaleString('en-US', {
                      minimumFractionDigits: 4,
                      maximumFractionDigits: 4,
                    }),
                },
                {
                  key: 'createdDate',
                  dataIndex: 'createdDate',
                  title: 'Created Date',
                  render: (value: string) => dayjs(value).format('YYYY-MM-DD'),
                },
              ]}
            />
          </Space>
        </Card>
      )}
    </>
  );
};

export default ImportLicenceDataImport;
