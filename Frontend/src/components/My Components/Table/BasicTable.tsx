import React, { useEffect, useMemo, useState } from 'react';
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
import {
  CaretDownOutlined,
  CaretUpOutlined,
  FileExcelOutlined,
} from '@ant-design/icons';
import * as XLSX from 'xlsx';

export type SortOrder = 'asc' | 'desc';

export interface BasicTableQuery {
  pageIndex: number;
  pageSize: number;
  sortColumn: string;
  sortOrder: SortOrder;
  filterColumn: string;
  filterQuery: string;
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
  render?: (value: unknown, row: T, rowIndex: number) => React.ReactNode;
}

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
  idleText?: string;
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
  initialSortColumn,
  initialSortOrder = 'desc',
  initialPageSize = 10,
  emptyText = 'No data',
  enabled = true,
  idleText = 'Set filters, then click Filter to load data',
}: PropsType<T>) => {
  const normalizedColumns = useMemo<BasicTableColumn<T>[]>(() => {
    if (columns?.length) {
      return columns.filter((column) => !column.hidden);
    }

    return (displayData ?? []).map((key) => ({
      key,
      title: NameConvert(key),
      sortable: key.toLocaleLowerCase() !== 'id',
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
    searchableColumns[0]?.sortKey ??
    searchableColumns[0]?.key.toString() ??
    '';

  const [loading, setLoading] = useState<boolean>(false);
  const [excelLoading, setExcelLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [sortColumn, setSortColumn] = useState(
    initialSortColumn ?? firstDataColumn
  );
  const [sortDirection, setSortDirection] =
    useState<SortOrder>(initialSortOrder);
  const [filterColumn, setFilterColumn] = useState(firstDataColumn);
  const [filterQuery] = useState('');
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [data, setData] = useState<PaginationType<T>>(emptyPage<T>());

  const shouldShowActions =
    showActions ??
    Boolean(actionComponent || (!fetchData && (displayData ?? []).includes('id')));

  useEffect(() => {
    if (!filterColumn && firstDataColumn) {
      setFilterColumn(firstDataColumn);
    }

    if (!sortColumn && firstDataColumn) {
      setSortColumn(firstDataColumn);
    }
  }, [filterColumn, firstDataColumn, sortColumn]);

  const query = useMemo<BasicTableQuery>(
    () => ({
      pageIndex: pageIndex < 0 ? 0 : pageIndex,
      pageSize,
      sortColumn,
      sortOrder: sortDirection,
      filterColumn,
      filterQuery,
    }),
    [filterColumn, filterQuery, pageIndex, pageSize, sortColumn, sortDirection]
  );

  const handleSort = (column: BasicTableColumn<T>) => {
    if (column.sortable === false) {
      return;
    }

    const nextColumn = column.sortKey ?? column.key.toString();
    setPageIndex(0);
    setSortColumn(nextColumn);
    setSortDirection((current) =>
      sortColumn === nextColumn && current === 'asc' ? 'desc' : 'asc'
    );
  };

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
        const result = fetchData
          ? await fetchData(query)
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

    if (column.render) {
      return column.render(value, row, index);
    }

    return value?.toString() ?? 'N/A';
  };

  const columnCount =
    1 + normalizedColumns.length + (shouldShowActions ? 1 : 0);
  const skeletonRowCount = Math.min(Math.max(pageSize, 5), 12);

  return (
    <>
      <div className="container">
        <Flex
          justify="space-between"
          align="center"
          style={{ paddingBottom: 16 }}
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
            disabled={!enabled}
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
              <tr>
                <th>No</th>
                {normalizedColumns.map((column) => {
                  const key = column.key.toString();
                  const sortKey = column.sortKey ?? key;
                  const isSorted = sortColumn === sortKey;

                  return (
                    <th
                      key={key}
                      onClick={() => handleSort(column)}
                      style={{
                        cursor: column.sortable === false ? 'default' : 'pointer',
                        width: column.width,
                      }}
                    >
                      {column.title ?? NameConvert(key)}
                      {isSorted && (
                        <span>
                          {sortDirection === 'asc' ? (
                            <CaretUpOutlined />
                          ) : (
                            <CaretDownOutlined />
                          )}
                        </span>
                      )}
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
                      <td>{index + 1 + query.pageIndex * pageSize}</td>
                      {normalizedColumns.map((column) => (
                        <td key={column.key.toString()}>
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
          </table>
        </div>
        <div className="pagination">
          <Pagination
            showSizeChanger
            pageSizeOptions={[10, 20, 50, 100, 1000]}
            defaultPageSize={initialPageSize}
            onShowSizeChange={(_, size) => {
              setPageIndex(0);
              setPageSize(size);
            }}
            current={query.pageIndex + 1}
            pageSize={pageSize}
            total={data.totalCount}
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
