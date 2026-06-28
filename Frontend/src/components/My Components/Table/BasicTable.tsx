import React, { useEffect, useMemo, useRef, useState } from 'react';
import './style.css';

import TableAction from '../TableAction/TableAction';
import {
  Alert,
  Button,
  Empty,
  Flex,
  Pagination,
  Skeleton,
  Typography,
} from 'antd';

import NameConvert from '../../../services/NameConvert';
import { AnyObject } from '../../../types/AnyObject';
import { PaginationType } from '../../../types/PaginationType';
import { ReportColumnDrilldown } from '../../../Report/config/reportTypes';
import { FileExcelOutlined } from '@ant-design/icons';
import * as XLSX from 'xlsx';

export type SortOrder = 'asc' | 'desc';

export interface BasicTableQuery {
  pageIndex: number;
  pageSize: number;
  sortColumn: string;
  sortOrder: string;
  filterColumn: string;
  filterQuery: string;
  includeTotalCount: boolean;
}

export interface BasicTableColumn<T extends AnyObject = AnyObject> {
  key: Extract<keyof T, string> | string;
  dataIndex?: Extract<keyof T, string> | string;
  sortKey?: string;
  filterKey?: string;
  title?: string;
  sortable?: boolean;
  searchable?: boolean;
  hidden?: boolean;
  width?: number | string;
  dataType?: 'string' | 'number' | 'date' | 'dateTime' | 'boolean' | 'money';
  render?: (value: unknown, row: T, rowIndex: number) => React.ReactNode;
  /** When set, the cell renders as a clickable link that drills into another report. */
  drilldown?: ReportColumnDrilldown;
}

const isNumericColumn = (dataType?: string) =>
  dataType === 'number' || dataType === 'money';

export type TableFunctionType = (api: string) => Promise<PaginationType>;

interface PropsType<T extends AnyObject = AnyObject> {
  displayData?: string[];
  columns?: BasicTableColumn<T>[];
  api?: string;
  fetch?: (url: string) => Promise<PaginationType<T>>;
  fetchData?: (query: BasicTableQuery) => Promise<PaginationType<T>>;
  actionComponent?: React.FC<{ id: string }>;
  title?: string;
  tableId?: string;
  rowKey?: Extract<keyof T, string> | ((row: T, index: number) => React.Key);
  showActions?: boolean;
  searchable?: boolean;
  extraFilters?: React.ReactNode;
  onExcel?: (query: BasicTableQuery) => Promise<void>;
  excelFileName?: string;
  refreshKey?: string | number;
  initialSortColumn?: string;
  initialSortOrder?: SortOrder;
  initialPageSize?: number;
  emptyText?: string;
  enabled?: boolean;
  lazyTotalCount?: boolean;
  /**
   * Controls the Excel button's enabled state independently of `enabled`
   * (which gates the grid fetch). Defaults to `enabled` so callers that only
   * set `enabled` keep the old "Excel after Filter" behavior. Pass `true` to
   * allow exporting without loading the grid first.
   */
  excelEnabled?: boolean;
  idleText?: string;
  showRowNumber?: boolean;
  rowNumberTitle?: string;
  legacyReportViewer?: boolean;
  /**
   * Optional centered heading lines rendered inside the table, spanning all
   * columns above the column-header row (mirrors the legacy RDLC report header).
   */
  reportHeaderLines?: string[];
  /**
   * Invoked when a drill-down cell is clicked, with the column's drilldown
   * descriptor and the clicked row. The host (GenericReportPage) navigates to
   * the target report with the mapped filters seeded.
   */
  onDrill?: (drilldown: ReportColumnDrilldown, row: AnyObject) => void;
  /**
   * Placement for the currency-grouped summary footer when the response carries
   * `currencyTotals`. `labelColumnKey` is the column.key under which the
   * `<CUR>: N licence(s)` text (and grand `Total: N licence(s)`) renders;
   * the summed value renders under `valueColumnKey`. Falls back to the first
   * text / first numeric column when omitted.
   */
  currencyTotalsColumns?: {
    labelColumnKey: string;
    valueColumnKey: string;
  };
}

const emptyPage = <T extends AnyObject>(): PaginationType<T> => ({
  data: [],
  pageIndex: 0,
  pageSize: 10,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
  sortColumn: '',
  sortOrder: '',
  filterColumn: '',
  filterQuery: '',
  isTotalCountExact: true,
});

const buildLegacyUrl = (api: string, query: BasicTableQuery) => {
  const params = new URLSearchParams({
    pageIndex: query.pageIndex.toString(),
    pageSize: query.pageSize.toString(),
  });

  if (query.sortColumn) {
    params.set('sortColumn', query.sortColumn);
    params.set('sortOrder', query.sortOrder);
  }

  if (query.filterColumn && query.filterQuery) {
    params.set('filterColumn', query.filterColumn);
    params.set('filterQuery', query.filterQuery);
  }

  return `${api}?${params.toString()}`;
};

export const BasicTable = <T extends AnyObject = AnyObject>({
  displayData,
  columns,
  api,
  fetch,
  fetchData,
  actionComponent,
  title = 'Table',
  tableId = 'reportTable',
  rowKey,
  showActions,
  onExcel,
  excelFileName = 'Report.xlsx',
  refreshKey,
  initialPageSize = 10,
  emptyText = 'No data',
  enabled = true,
  lazyTotalCount = true,
  excelEnabled = enabled,
  idleText = 'Set filters, then click Filter to load data',
  showRowNumber = true,
  rowNumberTitle = 'No',
  legacyReportViewer = false,
  reportHeaderLines,
  onDrill,
  currencyTotalsColumns,
}: PropsType<T>) => {
  const normalizedColumns = useMemo<BasicTableColumn<T>[]>(() => {
    if (columns?.length) {
      return columns.filter((column) => !column.hidden);
    }

    return (displayData ?? []).map((key) => ({
      key,
      title: NameConvert(key),
      searchable: key.toLocaleLowerCase() !== 'id',
    }));
  }, [columns, displayData]);

  const searchableColumns = useMemo(
    () =>
      normalizedColumns.filter(
        (column) =>
          column.searchable !== false &&
          column.key.toString().toLocaleLowerCase() !== 'id'
      ),
    [normalizedColumns]
  );

  const firstDataColumn =
    searchableColumns[0]?.filterKey ??
    searchableColumns[0]?.key.toString() ??
    '';

  const [loading, setLoading] = useState<boolean>(false);
  const [excelLoading, setExcelLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [filterColumn, setFilterColumn] = useState(firstDataColumn);
  const [filterQuery] = useState('');
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [data, setData] = useState<PaginationType<T>>(emptyPage<T>());
  // Lazy/partial delivery: rows render from a fast page that skips the expensive
  // COUNT(*); the exact total loads separately and patches the pager when ready.
  const [exactTotalCount, setExactTotalCount] = useState<number | null>(null);
  // Some reports (heavy drill lists) defer the per-currency footer to the lazy
  // exact-count request so the first page paints immediately; capture it here and
  // prefer it over the initial response's (absent) footer.
  const [lazyCurrencyTotals, setLazyCurrencyTotals] =
    useState<PaginationType<T>['currencyTotals']>(undefined);
  const [lazyColumnTotals, setLazyColumnTotals] =
    useState<PaginationType<T>['columnTotals']>(undefined);

  const shouldShowActions =
    showActions ??
    Boolean(actionComponent || (!fetchData && (displayData ?? []).includes('id')));

  useEffect(() => {
    if (!filterColumn && firstDataColumn) {
      setFilterColumn(firstDataColumn);
    }
  }, [filterColumn, firstDataColumn]);

  const query = useMemo<BasicTableQuery>(
    () => ({
      pageIndex: pageIndex < 0 ? 0 : pageIndex,
      pageSize,
      sortColumn: '',
      sortOrder: '',
      filterColumn,
      filterQuery,
      includeTotalCount: true,
    }),
    [filterColumn, filterQuery, pageIndex, pageSize]
  );

  useEffect(() => {
    const load = async () => {
      if (!enabled) {
        setLoading(false);
        setError(null);
        setData(emptyPage<T>());
        return;
      }

      if (!fetchData && (!fetch || !api)) {
        return;
      }

      setLoading(true);
      setError(null);

      try {
        // Report (fetchData) path: request a "fast page" that skips the
        // expensive COUNT(*) so the grid paints immediately; the exact total is
        // fetched lazily below. The legacy fetch/api path keeps the exact count.
        const result = fetchData
          ? await fetchData({ ...query, includeTotalCount: false })
          : await fetch!(buildLegacyUrl(api!, query));

        setData(result ?? emptyPage<T>());
      } catch {
        setError('Failed to load table data.');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [api, enabled, fetch, fetchData, query, refreshKey]);

  // Reset the lazily-fetched total whenever the filter set changes, so a stale
  // count never shows against new results.
  const countedFilterSig = useRef<string | null>(null);
  const filterSig = `${filterColumn}|${filterQuery}|${refreshKey}`;
  useEffect(() => {
    setExactTotalCount(null);
    setLazyCurrencyTotals(undefined);
    setLazyColumnTotals(undefined);
    countedFilterSig.current = null;
  }, [filterSig]);

  // Lazy total count (partial delivery). Only fires when the rows came back as an
  // ESTIMATED fast page (isTotalCountExact === false) — i.e. the heavy detail /
  // pagination reports. Aggregate reports already return an exact total, so they
  // are skipped (no double work). Fetched once per filter set, off the critical
  // path, via the fetchData (report) path only; the legacy fetch/api path is untouched.
  useEffect(() => {
    if (!enabled || !fetchData || !lazyTotalCount) {
      return;
    }
    if (data.isTotalCountExact !== false) {
      return;
    }
    if (countedFilterSig.current === filterSig) {
      return;
    }
    countedFilterSig.current = filterSig;

    let cancelled = false;
    const countQuery: BasicTableQuery = {
      pageIndex: 0,
      pageSize: 1,
      sortColumn: '',
      sortOrder: '',
      filterColumn,
      filterQuery,
      includeTotalCount: true,
    };

    fetchData(countQuery)
      .then((result) => {
        if (!cancelled && result) {
          setExactTotalCount(result.totalCount ?? null);
          // Heavy drill lists defer the per-currency footer to this request.
          if (result.currencyTotals) {
            setLazyCurrencyTotals(result.currencyTotals);
          }
          if (result.columnTotals) {
            setLazyColumnTotals(result.columnTotals);
          }
        }
      })
      .catch(() => {
        // Leave the estimated total from the fast page in place on failure.
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, fetchData, lazyTotalCount, data.isTotalCountExact, filterSig]);

  // Pager shows the exact total once it arrives, otherwise the fast page's
  // lower-bound estimate so Ant Pagination can still expose the next page.
  const displayTotalCount = exactTotalCount ?? data.totalCount;
  const isEstimatedTotalCount =
    data.isTotalCountExact === false && exactTotalCount === null;

  const exportClientTableToExcel = () => {
    const table = document.getElementById(tableId);
    if (!table) {
      return;
    }

    const workbook = XLSX.utils.table_to_book(table, { sheet: 'Report' });
    XLSX.writeFile(workbook, excelFileName);
  };

  const handleExcel = async () => {
    if (!onExcel) {
      exportClientTableToExcel();
      return;
    }

    setExcelLoading(true);
    setError(null);

    try {
      await onExcel(query);
    } catch {
      setError('Failed to generate Excel file.');
    } finally {
      setExcelLoading(false);
    }
  };

  const getRowKey = (row: T, index: number) => {
    if (typeof rowKey === 'function') {
      return rowKey(row, index);
    }

    return (rowKey ? row[rowKey] : row['id']) ?? `${pageIndex}-${index}`;
  };

  const renderCell = (column: BasicTableColumn<T>, row: T, index: number) => {
    const key = (column.dataIndex ?? column.key).toString();
    const value = row[key];

    const content = column.render
      ? column.render(value, row, index)
      : value?.toString() ?? 'N/A';

    // Drill-down cells (legacy RDLC blue hyperlinks) navigate to another report.
    // Only linkify when the cell has a real value and a handler is wired.
    const hasContent = value !== undefined && value !== null && value.toString().trim() !== '';
    if (column.drilldown && onDrill && hasContent) {
      return (
        <a
          className="report-drill-link"
          role="button"
          tabIndex={0}
          style={{ color: '#1677ff', cursor: 'pointer', textDecoration: 'underline' }}
          onClick={() => onDrill(column.drilldown!, row)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              onDrill(column.drilldown!, row);
            }
          }}
        >
          {column.drilldown.linkText ?? content}
        </a>
      );
    }

    return content;
  };

  const columnCount =
    (showRowNumber ? 1 : 0) +
    normalizedColumns.length +
    (shouldShowActions ? 1 : 0);
  const skeletonRowCount = Math.min(Math.max(pageSize, 5), 12);

  // Optional footer "Total" row, driven entirely by the per-column grand totals
  // the backend supplies (keyed by column dataIndex). Reports that don't send
  // columnTotals get no footer, so this is opt-in and backward compatible.
  const columnTotals = lazyColumnTotals ?? data.columnTotals ?? {};
  const hasColumnTotals = normalizedColumns.some(
    (column) => (column.dataIndex ?? column.key).toString() in columnTotals
  );
  const showTotalRow =
    !loading && (data.data?.length ?? 0) > 0 && hasColumnTotals;
  // Put the "Total" label in the first column that has no numeric total.
  const totalLabelIndex = normalizedColumns.findIndex(
    (column) =>
      !((column.dataIndex ?? column.key).toString() in columnTotals)
  );

  // Optional currency-grouped summary footer (legacy ExtensionReport.rdlc):
  // one "<CUR>: N licence(s)" + summed-value row per currency, then a grand
  // "Total: N licence(s)" row. Placement is config-driven (currencyTotalsColumns)
  // and falls back to the first text / first numeric column.
  const currencyTotals = lazyCurrencyTotals ?? data.currencyTotals;
  const showCurrencyTotals =
    !loading &&
    (data.data?.length ?? 0) > 0 &&
    (currencyTotals?.currencies?.length ?? 0) > 0;
  const currencyLabelColumnKey =
    currencyTotalsColumns?.labelColumnKey ??
    normalizedColumns
      .find((column) => !isNumericColumn(column.dataType))
      ?.key.toString();
  const currencyValueColumnKey =
    currencyTotalsColumns?.valueColumnKey ??
    normalizedColumns.find((column) => isNumericColumn(column.dataType))?.key.toString();
  // Matches the legacy RDLC FORMAT(Sum(Amount), "N4"): thousands separators and
  // exactly 4 decimal places.
  const formatCurrencyTotalValue = (value: number) =>
    Number(value).toLocaleString('en-US', {
      minimumFractionDigits: 4,
      maximumFractionDigits: 4,
    });
  const formatColumnTotalValue = (
    value: number,
    dataType?: BasicTableColumn<T>['dataType']
  ) => {
    if (dataType === 'money') {
      return Number(value).toLocaleString('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      });
    }

    if (dataType === 'number') {
      return Number(value).toLocaleString('en-US');
    }

    return String(value);
  };

  return (
    <>
      <div
        className={
          legacyReportViewer ? 'container report-viewer-container' : 'container'
        }
      >
        <Flex
          className={legacyReportViewer ? 'report-viewer-toolbar' : undefined}
          justify="space-between"
          align="center"
          style={legacyReportViewer ? undefined : { paddingBottom: 16 }}
          gap="small"
          wrap="wrap"
        >
          <Typography.Title level={5} style={{ margin: 0 }}>
            {title}
          </Typography.Title>

          <Button
            type="primary"
            icon={<FileExcelOutlined />}
            loading={excelLoading}
            disabled={!excelEnabled}
            onClick={handleExcel}
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
                  Preparing rows and totals...
                </Typography.Text>
              </Flex>
              <Skeleton.Button active size="small" className="loading-pill" />
            </Flex>
          )}

          <table id={tableId}>
            <thead>
              {reportHeaderLines
                ?.filter((line) => line?.trim())
                .map((line) => (
                  <tr key={line} className="report-header-row">
                    <th
                      colSpan={columnCount}
                      style={{ textAlign: 'center', fontWeight: 700 }}
                    >
                      {line}
                    </th>
                  </tr>
                ))}
              <tr>
                {showRowNumber && <th>{rowNumberTitle}</th>}
                {normalizedColumns.map((column) => {
                  const key = column.key.toString();

                  return (
                    <th
                      key={key}
                      className={
                        isNumericColumn(column.dataType) ? 'col-numeric' : undefined
                      }
                      style={{
                        width: column.width,
                      }}
                    >
                      {column.title ?? NameConvert(key)}
                    </th>
                  );
                })}
                {shouldShowActions && (
                  <th className="action-column-header">Action</th>
                )}
              </tr>
            </thead>

            {!loading && (
              <tbody>
                {data.data?.length ? (
                  data.data.map((row, index) => (
                    <tr key={getRowKey(row, index)}>
                      {showRowNumber && (
                        <td>{index + 1 + query.pageIndex * pageSize}</td>
                      )}
                      {normalizedColumns.map((column) => (
                        <td
                          key={column.key.toString()}
                          className={
                            isNumericColumn(column.dataType) ? 'col-numeric' : undefined
                          }
                        >
                          {renderCell(column, row, index)}
                        </td>
                      ))}
                      {shouldShowActions && (
                        <td className="action-column-cell">
                          {actionComponent ? (
                            React.createElement(actionComponent, {
                              id: row['id'],
                            })
                          ) : (
                            <TableAction id={row['id']} />
                          )}
                        </td>
                      )}
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={columnCount}>
                      <Empty description={enabled ? emptyText : idleText} />
                    </td>
                  </tr>
                )}
              </tbody>
            )}

            {loading && (
              <tbody className="table-skeleton-body" aria-busy="true">
                {Array.from({ length: skeletonRowCount }).map((_, rowIndex) => (
                  <tr key={rowIndex}>
                    {Array.from({ length: columnCount }).map((__, colIndex) => (
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
                    ))}
                  </tr>
                ))}
              </tbody>
            )}

            {(showTotalRow || showCurrencyTotals) && (
              <tfoot>
                {showTotalRow && (
                  <tr className="report-total-row">
                    {showRowNumber && <td />}
                    {normalizedColumns.map((column, index) => {
                      const dataIndex = (
                        column.dataIndex ?? column.key
                      ).toString();
                      const key = column.key.toString();
                      const total = columnTotals[dataIndex];

                      if (total !== undefined) {
                        return (
                          <td
                            key={key}
                            className={
                              isNumericColumn(column.dataType) ? 'col-numeric' : undefined
                            }
                            style={{ fontWeight: 700 }}
                          >
                            {formatColumnTotalValue(total, column.dataType)}
                          </td>
                        );
                      }

                      if (index === totalLabelIndex) {
                        return (
                          <td key={key} style={{ fontWeight: 700 }}>
                            Total
                          </td>
                        );
                      }

                      return <td key={key} />;
                    })}
                    {shouldShowActions && <td />}
                  </tr>
                )}

                {showCurrencyTotals &&
                  currencyTotals!.currencies.map((entry) => (
                    <tr
                      key={`currency-${entry.currency}`}
                      className="report-total-row"
                    >
                      {showRowNumber && <td />}
                      {normalizedColumns.map((column) => {
                        const key = column.key.toString();

                        if (key === currencyLabelColumnKey) {
                          return (
                            <td key={key} style={{ fontWeight: 700 }}>
                              {`${entry.currency}:${entry.noOfLicences} licence(s)`}
                            </td>
                          );
                        }

                        if (key === currencyValueColumnKey) {
                          return (
                            <td
                              key={key}
                              className="col-numeric"
                              style={{ fontWeight: 700 }}
                            >
                              {`${entry.currency}:${formatCurrencyTotalValue(entry.totalValue)}`}
                            </td>
                          );
                        }

                        return <td key={key} />;
                      })}
                      {shouldShowActions && <td />}
                    </tr>
                  ))}

                {showCurrencyTotals && (
                  <tr className="report-total-row">
                    {showRowNumber && (
                      <td style={{ fontWeight: 700 }}>TOTAL</td>
                    )}
                    {normalizedColumns.map((column, index) => {
                      const key = column.key.toString();

                      if (key === currencyLabelColumnKey) {
                        return (
                          <td key={key} style={{ fontWeight: 700 }}>
                            {`Total:${currencyTotals!.grandTotalLicences} licence(s)`}
                          </td>
                        );
                      }

                      if (
                        !showRowNumber &&
                        index === 0 &&
                        key !== currencyLabelColumnKey
                      ) {
                        return (
                          <td key={key} style={{ fontWeight: 700 }}>
                            TOTAL
                          </td>
                        );
                      }

                      return <td key={key} />;
                    })}
                    {shouldShowActions && <td />}
                  </tr>
                )}
              </tfoot>
            )}
          </table>
        </div>
        <div className="pagination">
          <Pagination
            showSizeChanger
            showTotal={(total, range) =>
              isEstimatedTotalCount
                ? `${range[0]}-${range[1]} of at least ${total} (calculating total)`
                : `${range[0]}-${range[1]} of ${total} total`
            }
            pageSizeOptions={[10, 20, 50, 100, 1000]}
            defaultPageSize={initialPageSize}
            onShowSizeChange={(_, size) => {
              setPageIndex(0);
              setPageSize(size);
            }}
            current={query.pageIndex + 1}
            pageSize={pageSize}
            total={displayTotalCount}
            onChange={(page, size) => {
              setPageIndex(page - 1);
              setPageSize(size);
            }}
          />
        </div>
      </div>
    </>
  );
};
