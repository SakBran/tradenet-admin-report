import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Modal, Space, Table, Typography } from 'antd';
import dayjs from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { AnyObject } from '../../types/AnyObject';
import { BasicTableColumn } from '../../components/My Components/Table/BasicTable';
import { reportConfigs } from '../config/reportConfigs';
import GenericReportPage from './GenericReportPage';

interface HistoryRow {
  id: string;
  createdDate: string | null;
  remark: string | null;
}

const ImportLicenceTurndownReport = ({
  border = false,
}: {
  border?: boolean;
}) => {
  const config =
    reportConfigs[`${border ? 'Border' : ''}ImportLicenceTurndownReport`];
  const [selected, setSelected] = useState<AnyObject | null>(null);
  const [history, setHistory] = useState<HistoryRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const pending = useRef<AbortController | null>(null);

  useEffect(() => () => pending.current?.abort(), []);
  useEffect(() => {
    pending.current?.abort();
    setSelected(null);
  }, [border]);

  const loadHistory = useCallback(
    async (row: AnyObject) => {
      pending.current?.abort();
      const controller = new AbortController();
      pending.current = controller;
      setSelected(row);
      setHistory([]);
      setError(false);
      setLoading(true);
      try {
        const response = await axiosInstance.get<HistoryRow[]>(
          `${config.apiRoute}/History/${encodeURIComponent(String(row.id))}`,
          { signal: controller.signal }
        );
        if (!controller.signal.aborted) setHistory(response.data);
      } catch {
        if (!controller.signal.aborted) setError(true);
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    },
    [config.apiRoute]
  );

  const cellRenderers = useMemo<
    Record<string, BasicTableColumn<AnyObject>['render']>
  >(
    () => ({
      TurndownCount: (value, row) => (
        <Button
          type="link"
          onClick={() => void loadHistory(row)}
          aria-label={`View ${String(value)} turndowns for ${String(row.applicationNo)}`}
        >
          {String(value)}
        </Button>
      ),
      TurndownRemark: (value) =>
        String(value ?? '').trim() || 'Remark unavailable',
    }),
    [loadHistory]
  );

  const closeHistory = () => {
    pending.current?.abort();
    setSelected(null);
  };

  return (
    <>
      <GenericReportPage
        key={config.controllerName}
        config={config}
        cellRenderers={cellRenderers}
      />
      <Modal
        title={`Turndown History — ${String(selected?.applicationNo ?? '')}`}
        open={selected !== null}
        onCancel={closeHistory}
        footer={null}
        width={850}
      >
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          <Typography.Text>
            Complete history across all dates. Recorded count:{' '}
            {String(selected?.turndownCount ?? 0)}.
          </Typography.Text>
          {error ? (
            <Alert
              type="error"
              showIcon
              message="Could not load turndown history."
              action={
                <Button onClick={() => selected && void loadHistory(selected)}>
                  Retry
                </Button>
              }
            />
          ) : (
            <Table<HistoryRow>
              rowKey="id"
              dataSource={history}
              loading={loading}
              pagination={false}
              scroll={{ x: 600, y: 440 }}
              locale={{ emptyText: 'No turndown history found.' }}
              columns={[
                {
                  title: 'No.',
                  width: 60,
                  render: (_value, _row, index) => index + 1,
                },
                {
                  title: 'Turndown Date / Created Date',
                  dataIndex: 'createdDate',
                  width: 210,
                  render: (value: string | null) =>
                    value
                      ? dayjs(value).format('DD/MM/YYYY HH:mm:ss')
                      : 'Date unavailable',
                },
                {
                  title: 'Remark',
                  dataIndex: 'remark',
                  render: (value: string | null) => (
                    <span
                      style={{
                        whiteSpace: 'pre-wrap',
                        overflowWrap: 'anywhere',
                      }}
                    >
                      {value?.trim() || 'Remark unavailable'}
                    </span>
                  ),
                },
              ]}
            />
          )}
          <Typography.Text type="secondary">
            Some historical remarks were cleared by the old system and are
            unavailable.
          </Typography.Text>
        </Space>
      </Modal>
    </>
  );
};

export default ImportLicenceTurndownReport;
