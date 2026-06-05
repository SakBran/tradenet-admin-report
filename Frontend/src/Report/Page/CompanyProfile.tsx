import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Flex,
  Form,
  Input,
  Pagination,
  Row,
  Skeleton,
  Space,
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
import { AnyObject } from '../../types/AnyObject';
import { PaginationType } from '../../types/PaginationType';

// CompanyProfile is rendered by this bespoke page (not GenericReportPage) so it can
// reproduce the legacy Tradenet 2.0 layout exactly: Myanmar column headers, the
// combined "RegNo / (date)" and single Address cells, and the nested
// "ဒါရိုက်တာအဖွဲ့၀င်များ" directors sub-grid with rowSpan-merged company cells.
// The backend (sp_CompanyProfileReport_pagination) pages at the COMPANY grain and
// returns one flat row per (company, director); we group those rows back into one
// block per company here. reportConfigs.CompanyProfile is kept only for the nav.

const API_ROUTE = 'CompanyProfile';
const EXCEL_ROUTE = 'CompanyProfile/Excel';
const EXCEL_FILE_NAME = 'CompanyProfile.xlsx';
const TABLE_ID = 'companyProfileTable';

// 9 company-level columns + 3 nested director columns.
const TOTAL_COLUMN_COUNT = 12;

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

interface CompanyProfileFilters {
  FromDate: string;
  ToDate: string;
  CompanyRegistrationNo: string;
}

interface CompanyProfileFormValues {
  dateRange: [Dayjs, Dayjs];
  CompanyRegistrationNo?: string;
}

interface DirectorEntry {
  directorName: string;
  directorNrc: string;
  directorPosition: string;
}

interface CompanyRow {
  id: string;
  companyRegistrationNo: string;
  companyName: string;
  companyRegistrationDate: string;
  endDate: string;
  businessType: string;
  unitLevel?: string;
  streetNumberStreetName?: string;
  quarterCityTownship?: string;
  state?: string;
  country?: string;
  postalCode?: string;
  capital?: number | string | null;
  permitBusiness?: string;
  extensionCount?: number;
  directors: DirectorEntry[];
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

const toFilters = (
  values: CompanyProfileFormValues
): CompanyProfileFilters => ({
  FromDate: toApiDate(values.dateRange[0], 'start'),
  ToDate: toApiDate(values.dateRange[1], 'end'),
  CompanyRegistrationNo: (values.CompanyRegistrationNo ?? '').trim(),
});

interface QueryState {
  pageIndex: number;
  pageSize: number;
}

const buildRequest = (filters: CompanyProfileFilters, query: QueryState) => ({
  ...filters,
  pageIndex: query.pageIndex,
  pageSize: query.pageSize,
  sortColumn: '',
  sortOrder: '',
  filterColumn: '',
  filterQuery: '',
  includeTotalCount: true,
});

const formatDate = (value: unknown) => {
  if (!value) {
    return '';
  }

  const parsed = dayjs(String(value));
  return parsed.isValid() ? parsed.format('DD/MM/YYYY') : String(value);
};

// Single combined address cell, matching the legacy "ကုမ္ပဏီလိပ်စာ" column which
// joined the address parts into one value.
const joinAddress = (company: CompanyRow) =>
  [
    company.unitLevel,
    company.streetNumberStreetName,
    company.quarterCityTownship,
    company.state,
    company.country,
    company.postalCode,
  ]
    .map((part) => (part ?? '').toString().trim())
    .filter(Boolean)
    .join(', ');

// The legacy report exploded the comma-separated permit businesses onto separate
// lines (Replace(PermitBusiness, ",", NewLine)).
const renderPermitBusiness = (permitBusiness?: string) => {
  const parts = (permitBusiness ?? '')
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean);

  if (!parts.length) {
    return '';
  }

  return parts.map((part, index) => <div key={index}>{part}</div>);
};

// Group the flat (company, director) rows the API returns into one block per
// company, preserving the server's order. Paging is at the company grain so a
// company's full set of directors is guaranteed to be on the same page.
const groupByCompany = (rows: AnyObject[]): CompanyRow[] => {
  const byId = new Map<string, CompanyRow>();
  const order: string[] = [];

  rows.forEach((row) => {
    const id = String(row.id ?? row.companyRegistrationNo ?? '');
    let company = byId.get(id);

    if (!company) {
      company = {
        id,
        companyRegistrationNo: String(row.companyRegistrationNo ?? ''),
        companyName: String(row.companyName ?? ''),
        companyRegistrationDate: String(row.companyRegistrationDate ?? ''),
        endDate: String(row.endDate ?? ''),
        businessType: String(row.businessType ?? ''),
        unitLevel: row.unitLevel as string | undefined,
        streetNumberStreetName: row.streetNumberStreetName as
          | string
          | undefined,
        quarterCityTownship: row.quarterCityTownship as string | undefined,
        state: row.state as string | undefined,
        country: row.country as string | undefined,
        postalCode: row.postalCode as string | undefined,
        capital: row.capital as number | string | null | undefined,
        permitBusiness: row.permitBusiness as string | undefined,
        extensionCount: row.extensionCount as number | undefined,
        directors: [],
      };
      byId.set(id, company);
      order.push(id);
    }

    company.directors.push({
      directorName: String(row.directorName ?? ''),
      directorNrc: String(row.directorNrc ?? ''),
      directorPosition: String(row.directorPosition ?? ''),
    });
  });

  return order.map((id) => byId.get(id)!);
};

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

const CompanyProfile = () => {
  const [form] = Form.useForm<CompanyProfileFormValues>();
  const initialFormValues = useMemo<CompanyProfileFormValues>(() => {
    const today = dayjs();
    return {
      dateRange: [today.startOf('month'), today],
      CompanyRegistrationNo: '',
    };
  }, []);

  const [filters, setFilters] = useState<CompanyProfileFilters>(() =>
    toFilters(initialFormValues)
  );
  const [hasAppliedFilters, setHasAppliedFilters] = useState(false);
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [excelLoading, setExcelLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState<PaginationType<AnyObject>>();
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    if (!hasAppliedFilters) {
      setPage(undefined);
      return;
    }

    let isCancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const response = await axiosInstance.post<PaginationType<AnyObject>>(
          API_ROUTE,
          buildRequest(filters, { pageIndex, pageSize })
        );

        if (!isCancelled) {
          setPage(response.data);
        }
      } catch {
        if (!isCancelled) {
          setError('Failed to load the report.');
        }
      } finally {
        if (!isCancelled) {
          setLoading(false);
        }
      }
    };

    load();

    return () => {
      isCancelled = true;
    };
  }, [filters, hasAppliedFilters, pageIndex, pageSize, refreshKey]);

  const companies = useMemo(() => groupByCompany(page?.data ?? []), [page]);

  const generateExcel = useCallback(async () => {
    let values: CompanyProfileFormValues;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }

    setExcelLoading(true);
    setError(null);

    try {
      const response = await axiosInstance.post<ExcelEnqueueResult>(
        EXCEL_ROUTE,
        buildRequest(toFilters(values), { pageIndex, pageSize })
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
    } catch {
      setError('Failed to generate Excel file.');
    } finally {
      setExcelLoading(false);
    }
  }, [form, pageIndex, pageSize]);

  const applyFilters = (values: CompanyProfileFormValues) => {
    setFilters(toFilters(values));
    setHasAppliedFilters(true);
    setPageIndex(0);
    setRefreshKey((current) => current + 1);
  };

  const resetFilters = () => {
    form.setFieldsValue(initialFormValues);
    setFilters(toFilters(initialFormValues));
    setHasAppliedFilters(false);
    setPageIndex(0);
    setRefreshKey((current) => current + 1);
  };

  // Legacy RDLC-style header, shown once filters are applied. Mirrors the old
  // header1 parameter "Company Profile ({FromDate}) To ({ToDate})".
  const reportHeaderLines = hasAppliedFilters
    ? [
        'Ministry of Commerce',
        'Directorate of Trade',
        `Company Profile (${dayjs(filters.FromDate).format(
          'DD/MM/YYYY'
        )}) To (${dayjs(filters.ToDate).format('DD/MM/YYYY')})`,
      ]
    : [];

  const skeletonRowCount = Math.min(Math.max(pageSize, 5), 12);

  return (
    <>
      <PageHeader title="Company Profile" />

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
              <Form.Item
                label="Company Registration No"
                name="CompanyRegistrationNo"
              >
                <Input allowClear placeholder="e.g. 143258106" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item label=" ">
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

      <div className="container">
        <Flex
          justify="space-between"
          align="center"
          style={{ paddingBottom: 16 }}
          gap="small"
          wrap="wrap"
        >
          <Typography.Title level={5} style={{ margin: 0 }}>
            Company Profile
          </Typography.Title>

          <Button
            type="primary"
            icon={<FileExcelOutlined />}
            loading={excelLoading}
            onClick={generateExcel}
          >
            Excel
          </Button>
        </Flex>

        {error && (
          <Alert
            type="error"
            message={error}
            showIcon
            style={{ marginBottom: 16 }}
          />
        )}

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
                <Typography.Text strong>Loading table data</Typography.Text>
                <Typography.Text type="secondary">
                  Preparing rows...
                </Typography.Text>
              </Flex>
              <Skeleton.Button active size="small" className="loading-pill" />
            </Flex>
          )}

          <table id={TABLE_ID}>
            <thead>
              {reportHeaderLines
                .filter((line) => line?.trim())
                .map((line) => (
                  <tr key={line} className="report-header-row">
                    <th
                      colSpan={TOTAL_COLUMN_COUNT}
                      style={{ textAlign: 'center', fontWeight: 700 }}
                    >
                      {line}
                    </th>
                  </tr>
                ))}
              <tr>
                <th rowSpan={2}>စဥ်</th>
                <th rowSpan={2}>ပသက / အမှတ်/ရက်စွဲ</th>
                <th rowSpan={2}>သက်တမ်းကုန်ဆုံးရက်</th>
                <th rowSpan={2}>ကုမ္ပဏီအမည်</th>
                <th rowSpan={2}>ကုမ္ပဏီလိပ်စာ</th>
                <th rowSpan={2}>ကုမ္ပဏီအမျိုးအစား</th>
                <th rowSpan={2}>လုပ်ငန်းရည်ရွယ်ချက်</th>
                <th rowSpan={2}>မတည်ငွေရင်း</th>
                <th rowSpan={2}>ပသက သက်တမ်းတိုး</th>
                <th colSpan={3} style={{ textAlign: 'center' }}>
                  ဒါရိုက်တာအဖွဲ့၀င်များ
                </th>
              </tr>
              <tr>
                <th>အမည်</th>
                <th>နိုင်ငံသားအမှတ်</th>
                <th>ရာထူး</th>
              </tr>
            </thead>

            {!loading && (
              <tbody>
                {companies.length ? (
                  companies.map((company, companyIndex) => {
                    const directors = company.directors.length
                      ? company.directors
                      : [
                          {
                            directorName: '',
                            directorNrc: '',
                            directorPosition: '',
                          },
                        ];
                    const serial = companyIndex + 1 + pageIndex * pageSize;
                    const address = joinAddress(company);

                    return directors.map((director, directorIndex) => (
                      <tr key={`${company.id}-${directorIndex}`}>
                        {directorIndex === 0 && (
                          <>
                            <td rowSpan={directors.length}>{serial}</td>
                            <td rowSpan={directors.length}>
                              <div>{company.companyRegistrationNo}</div>
                              {company.companyRegistrationDate && (
                                <div>
                                  ({formatDate(company.companyRegistrationDate)}
                                  )
                                </div>
                              )}
                            </td>
                            <td rowSpan={directors.length}>
                              {formatDate(company.endDate)}
                            </td>
                            <td rowSpan={directors.length}>
                              {company.companyName}
                            </td>
                            <td rowSpan={directors.length}>{address}</td>
                            <td rowSpan={directors.length}>
                              {company.businessType}
                            </td>
                            <td rowSpan={directors.length}>
                              {renderPermitBusiness(company.permitBusiness)}
                            </td>
                            <td
                              rowSpan={directors.length}
                              style={{ textAlign: 'right' }}
                            >
                              {company.capital !== null &&
                              company.capital !== undefined &&
                              company.capital !== ''
                                ? String(company.capital)
                                : ''}
                            </td>
                            <td
                              rowSpan={directors.length}
                              style={{ textAlign: 'center' }}
                            >
                              {company.extensionCount ?? 0}
                            </td>
                          </>
                        )}
                        <td>{director.directorName}</td>
                        <td>{director.directorNrc}</td>
                        <td>{director.directorPosition}</td>
                      </tr>
                    ));
                  })
                ) : (
                  <tr>
                    <td colSpan={TOTAL_COLUMN_COUNT}>
                      <Empty
                        description={
                          hasAppliedFilters
                            ? 'No data'
                            : 'Set filters, then click Filter to load the report.'
                        }
                      />
                    </td>
                  </tr>
                )}
              </tbody>
            )}

            {loading && (
              <tbody className="table-skeleton-body" aria-busy="true">
                {Array.from({ length: skeletonRowCount }).map((_, rowIndex) => (
                  <tr key={rowIndex}>
                    {Array.from({ length: TOTAL_COLUMN_COUNT }).map(
                      (__, colIndex) => (
                        <td key={colIndex}>
                          <Skeleton.Input
                            active
                            size="small"
                            className={
                              colIndex === 0
                                ? 'table-skeleton-index'
                                : 'table-skeleton-cell'
                            }
                          />
                        </td>
                      )
                    )}
                  </tr>
                ))}
              </tbody>
            )}
          </table>
        </div>

        <div className="pagination">
          <Pagination
            showSizeChanger
            showTotal={(total, range) =>
              `${range[0]}-${range[1]} of ${total} total`
            }
            pageSizeOptions={[10, 20, 50, 100, 1000]}
            current={pageIndex + 1}
            pageSize={pageSize}
            total={page?.totalCount ?? 0}
            onShowSizeChange={(_, size) => {
              setPageIndex(0);
              setPageSize(size);
            }}
            onChange={(nextPage, size) => {
              setPageIndex(nextPage - 1);
              setPageSize(size);
            }}
          />
        </div>
      </div>
    </>
  );
};

export default CompanyProfile;
