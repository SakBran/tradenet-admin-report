import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  Card,
  Popconfirm,
  Space,
  Table,
  Tag,
  Tooltip,
  message,
} from 'antd';
import {
  DeleteOutlined,
  DownloadOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { PageHeader } from '../../components';

type ExportJobStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed';

interface ExportJob {
  id: string;
  reportKey: string;
  reportTitle: string;
  status: ExportJobStatus;
  fileName: string;
  fileSizeBytes: number | null;
  rowCount: number | null;
  sheetCount: number | null;
  isPeriodClosed: boolean;
  requestedBy: string | null;
  errorMessage: string | null;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  expiresAtUtc: string;
  downloadUrl: string | null;
}

const statusColor: Record<ExportJobStatus, string> = {
  Queued: 'default',
  Processing: 'processing',
  Completed: 'success',
  Failed: 'error',
};

const formatBytes = (bytes: number | null): string => {
  if (bytes == null) return '-';
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(1)} ${units[unit]}`;
};

const formatDate = (value: string | null): string =>
  value ? dayjs(value).format('YYYY-MM-DD HH:mm') : '-';

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

const ExportsDrive = () => {
  const [jobs, setJobs] = useState<ExportJob[]>([]);
  const [loading, setLoading] = useState(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchJobs = useCallback(async () => {
    setLoading(true);
    try {
      const response = await axiosInstance.get<ExportJob[]>(
        'ExcelExport/jobs'
      );
      setJobs(response.data ?? []);
    } catch {
      message.error('Could not load exports.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchJobs();
  }, [fetchJobs]);

  // Poll while anything is still in progress so finished files appear automatically.
  const hasPending = useMemo(
    () => jobs.some((j) => j.status === 'Queued' || j.status === 'Processing'),
    [jobs]
  );

  useEffect(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
    if (hasPending) {
      timerRef.current = setInterval(fetchJobs, 5000);
    }
    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [hasPending, fetchJobs]);

  const handleDownload = useCallback(async (job: ExportJob) => {
    if (!job.downloadUrl) return;
    setDownloadingId(job.id);
    try {
      const response = await axiosInstance.get(job.downloadUrl, {
        responseType: 'blob',
      });
      const blob = new Blob([response.data], {
        type: String(
          response.headers['content-type'] ??
            'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        ),
      });
      downloadBlob(blob, job.fileName);
    } catch (error: unknown) {
      const status = (error as { response?: { status?: number } })?.response
        ?.status;
      if (status === 410) {
        message.error(
          'The export file is no longer available. Please regenerate it.'
        );
      } else {
        message.error('Could not download the export.');
      }
    } finally {
      setDownloadingId(null);
    }
  }, []);

  const handleDelete = useCallback(
    async (job: ExportJob) => {
      try {
        await axiosInstance.delete(`ExcelExport/${job.id}`);
        message.success('Export deleted.');
        fetchJobs();
      } catch {
        message.error('Could not delete the export.');
      }
    },
    [fetchJobs]
  );

  const columns: ColumnsType<ExportJob> = useMemo(
    () => [
      {
        title: 'Report',
        dataIndex: 'reportTitle',
        key: 'reportTitle',
        render: (title: string, job) => (
          <Space direction="vertical" size={0}>
            <span>{title}</span>
            <span style={{ fontSize: 12, color: '#999' }}>{job.fileName}</span>
          </Space>
        ),
      },
      {
        title: 'Status',
        dataIndex: 'status',
        key: 'status',
        render: (status: ExportJobStatus, job) =>
          status === 'Failed' && job.errorMessage ? (
            <Tooltip title={job.errorMessage}>
              <Tag color={statusColor[status]}>{status}</Tag>
            </Tooltip>
          ) : (
            <Tag color={statusColor[status]}>{status}</Tag>
          ),
      },
      {
        title: 'Period',
        dataIndex: 'isPeriodClosed',
        key: 'isPeriodClosed',
        render: (closed: boolean) =>
          closed ? <Tag>Historical</Tag> : <Tag color="blue">Up to date</Tag>,
      },
      {
        title: 'Rows',
        dataIndex: 'rowCount',
        key: 'rowCount',
        render: (rows: number | null) => (rows == null ? '-' : rows.toLocaleString()),
      },
      {
        title: 'Size',
        dataIndex: 'fileSizeBytes',
        key: 'fileSizeBytes',
        render: formatBytes,
      },
      {
        title: 'Generated by',
        dataIndex: 'requestedBy',
        key: 'requestedBy',
        render: (by: string | null) => by ?? '-',
      },
      {
        title: 'Created',
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        render: formatDate,
      },
      {
        title: 'Expires',
        dataIndex: 'expiresAtUtc',
        key: 'expiresAtUtc',
        render: formatDate,
      },
      {
        title: 'Actions',
        key: 'actions',
        render: (_, job) => (
          <Space>
            <Button
              type="primary"
              size="small"
              icon={<DownloadOutlined />}
              disabled={job.status !== 'Completed'}
              loading={downloadingId === job.id}
              onClick={() => handleDownload(job)}
            >
              Download
            </Button>
            <Popconfirm
              title="Delete this export?"
              onConfirm={() => handleDelete(job)}
              okText="Delete"
              cancelText="Cancel"
            >
              <Button danger size="small" icon={<DeleteOutlined />} />
            </Popconfirm>
          </Space>
        ),
      },
    ],
    [downloadingId, handleDownload, handleDelete]
  );

  return (
    <>
      <PageHeader title="Exports" />
      <Card>
        <Space style={{ marginBottom: 16 }}>
          <Button icon={<ReloadOutlined />} onClick={fetchJobs} loading={loading}>
            Refresh
          </Button>
        </Space>
        <Table<ExportJob>
          rowKey="id"
          columns={columns}
          dataSource={jobs}
          loading={loading}
          pagination={{ pageSize: 20, showSizeChanger: true }}
        />
      </Card>
    </>
  );
};

export default ExportsDrive;
