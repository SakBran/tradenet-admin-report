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
import { buildCompanyProfileExcelSpec } from '../excel/bespoke/companyProfile';
import { enqueueExcelExport } from '../excel/excelEnqueue';

// CompanyProfile is rendered by this bespoke page (not GenericReportPage) so it can
// print the layout the customer sends to the 11 ministries (complaint 2026-09-25;
// it replaced the legacy Tradenet 2.0 Myanmar-header layout): composed
// "name / reg no / (date)" and "EIR no / validity" cells, and the "Board of Director"
// Name / NRC No. band with rowSpan-merged company cells. The Excel export draws the
// same sheet (CompanyProfileController.GetExcelLayout).
// The backend (sp_CompanyProfileReport_pagination) pages at the COMPANY grain and
// returns one flat row per (company, director), with the address, validity, capital
// and title text already formatted; we group those rows back into one block per
// company here. reportConfigs.CompanyProfile is kept only for the nav.

const API_ROUTE = 'CompanyProfile';
const EXCEL_ROUTE = 'CompanyProfile/Excel';
const EXCEL_FILE_NAME = 'CompanyProfile.xlsx';
const TABLE_ID = 'companyProfileTable';

// 7 company-level columns + the 2-column "Board of Director" band + Title.
const TOTAL_COLUMN_COUNT = 10;

// The customer's sample centres every header cell, band included.
const HEADER_CELL_STYLE = {
  textAlign: 'center',
  verticalAlign: 'middle',
} as const;

// The index signature makes these the "applied filters" record the bespoke Excel spec
// builder reads (it formats FromDate/ToDate into the sheet's header line).
interface CompanyProfileFilters {
  [key: string]: unknown;
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
  directorTitle: string;
}

interface CompanyRow {
  id: string;
  companyRegistrationNo: string;
  companyName: string;
  companyRegistrationDate: string;
  companyAddress: string;
  eirValidity: string;
  businessType: string;
  permitBusiness?: string;
  capitalText: string;
  directors: DirectorEntry[];
}

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
        companyAddress: String(row.companyAddress ?? ''),
        eirValidity: String(row.eirValidity ?? ''),
        businessType: String(row.businessType ?? ''),
        permitBusiness: row.permitBusiness as string | undefined,
        capitalText: String(row.capitalText ?? ''),
        directors: [],
      };
      byId.set(id, company);
      order.push(id);
    }

    company.directors.push({
      directorName: String(row.directorName ?? ''),
      directorNrc: String(row.directorNrc ?? ''),
      directorTitle: String(row.directorTitle ?? ''),
    });
  });

  return order.map((id) => byId.get(id)!);
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
      // The endpoint REJECTS a request that carries no presentation spec, which is why this
      // export always failed. buildCompanyProfileExcelSpec describes this page's hand-built
      // Myanmar columns (the generic builder would describe the nav-only config instead).
      const applied = toFilters(values);
      await enqueueExcelExport(
        EXCEL_ROUTE,
        buildRequest(applied, { pageIndex, pageSize }),
        buildCompanyProfileExcelSpec(applied),
        EXCEL_FILE_NAME
      );
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
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  No
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  Company&apos;s Name
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  Address
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  EIR No. &amp; Date
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  Type of Organization
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  လုပ်ငန်းရည်ရွယ်ချက်
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  Capital
                </th>
                <th colSpan={2} style={HEADER_CELL_STYLE}>
                  Board of Director
                </th>
                <th rowSpan={2} style={HEADER_CELL_STYLE}>
                  Title
                </th>
              </tr>
              <tr>
                <th style={HEADER_CELL_STYLE}>Name</th>
                <th style={HEADER_CELL_STYLE}>NRC No.</th>
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
                            directorTitle: '',
                          },
                        ];
                    const serial = companyIndex + 1 + pageIndex * pageSize;

                    return directors.map((director, directorIndex) => (
                      <tr key={`${company.id}-${directorIndex}`}>
                        {directorIndex === 0 && (
                          <>
                            <td
                              rowSpan={directors.length}
                              style={{ textAlign: 'center' }}
                            >
                              {serial}
                            </td>
                            <td rowSpan={directors.length}>
                              <div>{company.companyName}</div>
                              <div>{company.companyRegistrationNo}</div>
                              {company.companyRegistrationDate && (
                                <div>
                                  ({formatDate(company.companyRegistrationDate)}
                                  )
                                </div>
                              )}
                            </td>
                            <td rowSpan={directors.length}>
                              {company.companyAddress}
                            </td>
                            <td
                              rowSpan={directors.length}
                              style={{ textAlign: 'center' }}
                            >
                              <div>{company.companyRegistrationNo}</div>
                              <div>{company.eirValidity}</div>
                            </td>
                            <td
                              rowSpan={directors.length}
                              style={{ textAlign: 'center' }}
                            >
                              {company.businessType}
                            </td>
                            <td rowSpan={directors.length}>
                              {renderPermitBusiness(company.permitBusiness)}
                            </td>
                            <td
                              rowSpan={directors.length}
                              style={{ textAlign: 'center' }}
                            >
                              {company.capitalText}
                            </td>
                          </>
                        )}
                        <td>{director.directorName}</td>
                        <td>{director.directorNrc}</td>
                        <td>{director.directorTitle}</td>
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
