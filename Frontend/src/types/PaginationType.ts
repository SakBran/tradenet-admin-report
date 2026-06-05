import { AnyObject } from './AnyObject';

export interface PaginationType<T extends AnyObject = AnyObject> {
  data: T[];
  pageIndex: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  isTotalCountExact?: boolean;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
  sortColumn: string;
  sortOrder: string;
  filterColumn: string;
  filterQuery: string;
  /**
   * Optional per-column grand totals, keyed by column dataIndex (e.g.
   * "companyCount"). When present, BasicTable renders a footer "Total" row.
   */
  columnTotals?: Record<string, number>;
}


