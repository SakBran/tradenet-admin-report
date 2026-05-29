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
}


