import {
  CSSProperties,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import {
  Button,
  Card,
  DatePicker,
  Form,
  Progress,
  Select,
  Space,
  Table,
  Tabs,
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

const importFormGridStyle: CSSProperties = {
  alignItems: 'end',
  display: 'grid',
  gap: 16,
  gridTemplateColumns: 'minmax(220px, 280px) minmax(320px, 420px) auto',
  maxWidth: 980,
};

const importFormItemStyle: CSSProperties = {
  marginBottom: 0,
};

type FormValues = {
  licenceType: string;
  dateRange: [Dayjs, Dayjs];
};

type DataImportJobStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed';

type DataImportJob = {
  id: string;
  licenceType: string;
  startDate: string;
  endDate: string;
  status: DataImportJobStatus;
  totalDays: number;
  processedDays: number;
  progressPercent: number;
  totalRows: number;
  requestedBy: string | null;
  errorMessage: string | null;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
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

const jobStatusColor: Record<DataImportJobStatus, string> = {
  Queued: 'default',
  Processing: 'processing',
  Completed: 'success',
  Failed: 'error',
};

const licenceTypeLabelMap = new Map(
  licenceTypeOptions.map((option) => [option.value, option.label])
);

const formatDateTime = (value: string | null): string =>
  value ? dayjs(value).format('YYYY-MM-DD HH:mm') : '-';

const AnimatedImportProgress = ({
  percent,
  status,
}: {
  percent: number;
  status?: DataImportJobStatus;
}) => {
  const safePercent = Math.max(0, Math.min(100, Math.round(percent)));
  const isProcessing = status === 'Processing';
  const isCompleted = status === 'Completed';
  const isFailed = status === 'Failed';
  const showCompleteScene = !isFailed && (isCompleted || safePercent >= 100);
  const showChaseScene = isProcessing && safePercent > 0 && safePercent < 100;

  return (
    <>
      <style>
        {`
          .data-import-progress {
            --progress: 0%;
            position: relative;
            padding: 42px 82px 4px 4px;
          }

          .data-import-progress__track {
            position: relative;
            height: 18px;
            overflow: hidden;
            border: 1px solid #d9d9d9;
            border-radius: 999px;
            background: #f5f5f5;
          }

          .data-import-progress__fill {
            position: absolute;
            inset: 0 auto 0 0;
            width: var(--progress);
            border-radius: inherit;
            background: linear-gradient(90deg, #1677ff, #13c2c2);
            transition: width 420ms ease;
          }

          .data-import-progress__fill::after {
            content: '';
            position: absolute;
            inset: 0;
            background: repeating-linear-gradient(
              -45deg,
              rgba(255, 255, 255, 0.28) 0,
              rgba(255, 255, 255, 0.28) 10px,
              transparent 10px,
              transparent 20px
            );
            animation: import-stripes 900ms linear infinite;
          }

          .data-import-progress--completed .data-import-progress__fill {
            background: linear-gradient(90deg, #52c41a, #95de64);
          }

          .data-import-progress--failed .data-import-progress__fill {
            background: linear-gradient(90deg, #ff4d4f, #ff7875);
          }

          .data-import-progress__runner {
            position: absolute;
            left: var(--progress);
            top: 16px;
            width: 32px;
            height: 28px;
            transform: translateX(-50%);
            transition: left 420ms ease;
            z-index: 2;
          }

          .data-import-progress__runner,
          .data-import-progress__dogs,
          .data-import-progress__destination {
            opacity: 0;
            pointer-events: none;
          }

          .data-import-progress--chasing .data-import-progress__runner,
          .data-import-progress--chasing .data-import-progress__dogs,
          .data-import-progress--completed .data-import-progress__destination,
          .data-import-progress--complete .data-import-progress__destination {
            opacity: 1;
          }

          .data-import-progress--completed .data-import-progress__runner,
          .data-import-progress--complete .data-import-progress__runner {
            opacity: 0;
            pointer-events: none;
            transform: translateX(-50%) translateY(8px) scale(0.72);
            transition: left 420ms ease, opacity 260ms ease, transform 260ms ease;
          }

          .data-import-progress__head,
          .data-import-progress__body,
          .data-import-progress__arm,
          .data-import-progress__leg,
          .data-import-progress__pack,
          .data-import-progress__sweat,
          .data-import-progress__dog,
          .data-import-progress__dog-body,
          .data-import-progress__dog-head,
          .data-import-progress__dog-ear,
          .data-import-progress__dog-tail,
          .data-import-progress__dog-leg,
          .data-import-progress__dog-bark {
            position: absolute;
            display: block;
          }

          .data-import-progress__head {
            left: 15px;
            top: 0;
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background: #262626;
          }

          .data-import-progress__body {
            left: 14px;
            top: 8px;
            width: 6px;
            height: 13px;
            border-radius: 999px;
            background: #262626;
            transform: rotate(15deg);
            transform-origin: top center;
          }

          .data-import-progress__pack {
            left: 6px;
            top: 9px;
            width: 10px;
            height: 10px;
            border-radius: 3px;
            background: #faad14;
            box-shadow: inset 0 -2px 0 rgba(0, 0, 0, 0.16);
            transform: rotate(-8deg);
          }

          .data-import-progress__arm,
          .data-import-progress__leg {
            width: 4px;
            height: 13px;
            border-radius: 999px;
            background: #262626;
            transform-origin: top center;
          }

          .data-import-progress__arm--front {
            left: 18px;
            top: 10px;
            transform: rotate(-46deg);
          }

          .data-import-progress__arm--back {
            left: 13px;
            top: 10px;
            transform: rotate(54deg);
          }

          .data-import-progress__leg--front {
            left: 18px;
            top: 19px;
            transform: rotate(-42deg);
          }

          .data-import-progress__leg--back {
            left: 14px;
            top: 19px;
            transform: rotate(48deg);
          }

          .data-import-progress__sweat {
            left: 25px;
            top: 2px;
            width: 4px;
            height: 6px;
            border-radius: 999px;
            background: #69c0ff;
            opacity: 0;
            transform: rotate(22deg);
          }

          .data-import-progress--processing .data-import-progress__runner {
            animation: import-runner-bob 520ms ease-in-out infinite;
          }

          .data-import-progress--processing .data-import-progress__arm--front,
          .data-import-progress--processing .data-import-progress__leg--back {
            animation: import-limb-front 520ms ease-in-out infinite;
          }

          .data-import-progress--processing .data-import-progress__arm--back,
          .data-import-progress--processing .data-import-progress__leg--front {
            animation: import-limb-back 520ms ease-in-out infinite;
          }

          .data-import-progress--processing .data-import-progress__sweat {
            animation: import-sweat 900ms ease-in-out infinite;
          }

          .data-import-progress__dogs {
            position: absolute;
            left: var(--progress);
            top: 22px;
            width: 72px;
            height: 30px;
            transform: translateX(-92px);
            transition: left 420ms ease;
            z-index: 1;
            pointer-events: none;
          }

          .data-import-progress__dog {
            width: 28px;
            height: 20px;
            transform-origin: center bottom;
          }

          .data-import-progress__dog--one {
            left: 28px;
            top: 5px;
          }

          .data-import-progress__dog--two {
            left: 0;
            top: 8px;
            transform: scale(0.86);
            opacity: 0.9;
          }

          .data-import-progress__dog-body {
            left: 5px;
            top: 8px;
            width: 17px;
            height: 9px;
            border-radius: 999px;
            background: #8c5a2b;
          }

          .data-import-progress__dog-head {
            right: 0;
            top: 4px;
            width: 10px;
            height: 9px;
            border-radius: 50% 50% 45% 45%;
            background: #8c5a2b;
          }

          .data-import-progress__dog-head::after {
            content: '';
            position: absolute;
            right: -3px;
            top: 4px;
            width: 4px;
            height: 3px;
            border-radius: 999px;
            background: #262626;
          }

          .data-import-progress__dog-ear {
            right: 5px;
            top: 1px;
            width: 5px;
            height: 7px;
            border-radius: 999px;
            background: #5c3518;
            transform: rotate(26deg);
          }

          .data-import-progress__dog-tail {
            left: 1px;
            top: 7px;
            width: 8px;
            height: 3px;
            border-radius: 999px;
            background: #8c5a2b;
            transform: rotate(-34deg);
            transform-origin: right center;
          }

          .data-import-progress__dog-leg {
            top: 15px;
            width: 3px;
            height: 6px;
            border-radius: 999px;
            background: #5c3518;
            transform-origin: top center;
          }

          .data-import-progress__dog-leg--front {
            left: 18px;
            transform: rotate(-22deg);
          }

          .data-import-progress__dog-leg--back {
            left: 8px;
            transform: rotate(24deg);
          }

          .data-import-progress__dog-bark {
            right: -22px;
            top: -11px;
            color: #d46b08;
            font-size: 9px;
            font-weight: 700;
            line-height: 1;
            opacity: 0;
          }

          .data-import-progress--processing .data-import-progress__dogs {
            animation: import-dog-pack 640ms ease-in-out infinite;
          }

          .data-import-progress--processing .data-import-progress__dog--one {
            animation: import-dog-lunge 420ms ease-in-out infinite;
          }

          .data-import-progress--processing .data-import-progress__dog--two {
            animation: import-dog-lunge 520ms ease-in-out 120ms infinite;
          }

          .data-import-progress--processing .data-import-progress__dog-tail {
            animation: import-dog-tail 240ms ease-in-out infinite;
          }

          .data-import-progress--processing .data-import-progress__dog-bark {
            animation: import-dog-bark 860ms ease-in-out infinite;
          }

          .data-import-progress--completed .data-import-progress__dogs,
          .data-import-progress--complete .data-import-progress__dogs,
          .data-import-progress--failed .data-import-progress__dogs {
            opacity: 0;
            transform: translateX(-110px) translateY(10px) scale(0.72);
            transition: left 420ms ease, opacity 220ms ease, transform 220ms ease;
          }

          .data-import-progress__label {
            display: flex;
            justify-content: flex-end;
            margin-top: 6px;
            color: #595959;
            font-size: 12px;
          }

          .data-import-progress__destination {
            position: absolute;
            right: 2px;
            top: 0;
            width: 78px;
            height: 68px;
            transition: opacity 260ms ease;
          }

          .data-import-progress__building {
            position: absolute;
            right: 7px;
            bottom: 0;
            width: 58px;
            height: 42px;
            border: 1px solid #9fb5d1;
            border-radius: 5px 5px 2px 2px;
            background:
              repeating-linear-gradient(
                90deg,
                rgba(255, 255, 255, 0.22) 0,
                rgba(255, 255, 255, 0.22) 6px,
                transparent 6px,
                transparent 12px
              ),
              linear-gradient(180deg, #ffffff, #d9ecff);
            box-shadow: 0 8px 18px rgba(22, 119, 255, 0.18);
          }

          .data-import-progress__building::before {
            content: '';
            position: absolute;
            left: 50%;
            top: -12px;
            width: 42px;
            height: 12px;
            border: 1px solid #9fb5d1;
            border-bottom: 0;
            border-radius: 3px 3px 0 0;
            background: #f0f7ff;
            transform: translateX(-50%);
          }

          .data-import-progress__building::after {
            content: '';
            position: absolute;
            left: 50%;
            top: -20px;
            width: 28px;
            height: 8px;
            border: 1px solid #9fb5d1;
            border-bottom: 0;
            border-radius: 3px 3px 0 0;
            background: #ffffff;
            transform: translateX(-50%);
          }

          .data-import-progress__building-label {
            position: absolute;
            left: 50%;
            top: 6px;
            color: #0958d9;
            font-size: 11px;
            font-weight: 700;
            letter-spacing: 0;
            transform: translateX(-50%);
          }

          .data-import-progress__door {
            position: absolute;
            left: 50%;
            bottom: 0;
            width: 12px;
            height: 15px;
            border-radius: 2px 2px 0 0;
            background: #1677ff;
            transform: translateX(-50%);
          }

          .data-import-progress__window {
            position: absolute;
            top: 21px;
            width: 7px;
            height: 7px;
            border-radius: 2px;
            background: #91caff;
          }

          .data-import-progress__window--left {
            left: 8px;
          }

          .data-import-progress__window--right {
            right: 8px;
          }

          .data-import-progress__firework {
            position: absolute;
            width: 6px;
            height: 6px;
            border-radius: 50%;
            opacity: 0;
            transform: scale(0);
            pointer-events: none;
          }

          .data-import-progress__firework--one {
            right: 42px;
            top: 2px;
            background: #ff4d4f;
            box-shadow:
              0 -13px 0 #ff4d4f,
              10px -9px 0 #faad14,
              13px 0 0 #52c41a,
              9px 10px 0 #1677ff,
              0 13px 0 #722ed1,
              -10px 9px 0 #13c2c2,
              -13px 0 0 #eb2f96,
              -9px -10px 0 #fa8c16;
          }

          .data-import-progress__firework--two {
            right: 4px;
            top: 0;
            background: #1677ff;
            box-shadow:
              0 -11px 0 #1677ff,
              8px -8px 0 #13c2c2,
              11px 0 0 #faad14,
              8px 8px 0 #52c41a,
              0 11px 0 #ff4d4f,
              -8px 8px 0 #722ed1,
              -11px 0 0 #eb2f96,
              -8px -8px 0 #fa8c16;
          }

          .data-import-progress--completed .data-import-progress__firework,
          .data-import-progress--complete .data-import-progress__firework {
            animation: import-firework 1200ms ease-out infinite;
          }

          .data-import-progress--completed .data-import-progress__firework--two,
          .data-import-progress--complete .data-import-progress__firework--two {
            animation-delay: 420ms;
          }

          @keyframes import-stripes {
            from { transform: translateX(0); }
            to { transform: translateX(28px); }
          }

          @keyframes import-runner-bob {
            0%, 100% { transform: translateX(-50%) translateY(0) rotate(-2deg); }
            50% { transform: translateX(-50%) translateY(-3px) rotate(4deg); }
          }

          @keyframes import-limb-front {
            0%, 100% { transform: rotate(-52deg); }
            50% { transform: rotate(44deg); }
          }

          @keyframes import-limb-back {
            0%, 100% { transform: rotate(52deg); }
            50% { transform: rotate(-44deg); }
          }

          @keyframes import-sweat {
            0% { opacity: 0; transform: translate(0, 0) rotate(22deg); }
            30% { opacity: 1; }
            100% { opacity: 0; transform: translate(8px, 8px) rotate(22deg); }
          }

          @keyframes import-dog-pack {
            0%, 100% { transform: translateX(-92px) translateY(0); }
            50% { transform: translateX(-86px) translateY(-2px); }
          }

          @keyframes import-dog-lunge {
            0%, 100% { transform: translateX(0) rotate(-2deg); }
            50% { transform: translateX(6px) rotate(5deg); }
          }

          @keyframes import-dog-tail {
            0%, 100% { transform: rotate(-42deg); }
            50% { transform: rotate(22deg); }
          }

          @keyframes import-dog-bark {
            0% { opacity: 0; transform: translate(0, 4px) scale(0.75); }
            20% { opacity: 1; transform: translate(3px, 0) scale(1); }
            70% { opacity: 1; }
            100% { opacity: 0; transform: translate(10px, -7px) scale(1.12); }
          }

          @keyframes import-firework {
            0% { opacity: 0; transform: scale(0); }
            18% { opacity: 1; transform: scale(0.7); }
            62% { opacity: 1; transform: scale(1.18); }
            100% { opacity: 0; transform: scale(1.45); }
          }

        `}
      </style>
      <div
        className={[
          'data-import-progress',
          showChaseScene ? 'data-import-progress--chasing' : '',
          isProcessing ? 'data-import-progress--processing' : '',
          isCompleted ? 'data-import-progress--completed' : '',
          showCompleteScene ? 'data-import-progress--complete' : '',
          isFailed ? 'data-import-progress--failed' : '',
        ]
          .filter(Boolean)
          .join(' ')}
        style={{ '--progress': `${safePercent}%` } as CSSProperties}
      >
        <div className="data-import-progress__runner" aria-hidden="true">
          <span className="data-import-progress__pack" />
          <span className="data-import-progress__head" />
          <span className="data-import-progress__body" />
          <span className="data-import-progress__arm data-import-progress__arm--front" />
          <span className="data-import-progress__arm data-import-progress__arm--back" />
          <span className="data-import-progress__leg data-import-progress__leg--front" />
          <span className="data-import-progress__leg data-import-progress__leg--back" />
          <span className="data-import-progress__sweat" />
        </div>
        <div className="data-import-progress__dogs" aria-hidden="true">
          {['one', 'two'].map((dog) => (
            <span
              key={dog}
              className={`data-import-progress__dog data-import-progress__dog--${dog}`}
            >
              <span className="data-import-progress__dog-bark">WOOF</span>
              <span className="data-import-progress__dog-tail" />
              <span className="data-import-progress__dog-body" />
              <span className="data-import-progress__dog-head" />
              <span className="data-import-progress__dog-ear" />
              <span className="data-import-progress__dog-leg data-import-progress__dog-leg--front" />
              <span className="data-import-progress__dog-leg data-import-progress__dog-leg--back" />
            </span>
          ))}
        </div>
        <div className="data-import-progress__destination" aria-hidden="true">
          <span className="data-import-progress__firework data-import-progress__firework--one" />
          <span className="data-import-progress__firework data-import-progress__firework--two" />
          <span className="data-import-progress__building">
            <span className="data-import-progress__building-label">MOC</span>
            <span className="data-import-progress__window data-import-progress__window--left" />
            <span className="data-import-progress__window data-import-progress__window--right" />
            <span className="data-import-progress__door" />
          </span>
        </div>
        <div className="data-import-progress__track">
          <span className="data-import-progress__fill" />
        </div>
        <div className="data-import-progress__label">{safePercent}%</div>
      </div>
    </>
  );
};

const ImportLicenceDataImport = () => {
  const [form] = Form.useForm<FormValues>();
  const [saving, setSaving] = useState(false);
  const [activeJob, setActiveJob] = useState<DataImportJob | null>(null);
  const [jobs, setJobs] = useState<DataImportJob[]>([]);
  const [loadingJobs, setLoadingJobs] = useState(false);
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [statusYear, setStatusYear] = useState(dayjs().year());
  const [calendarStatus, setCalendarStatus] =
    useState<CalendarStatusResult | null>(null);
  const pollingTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

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

  const isActiveJobPending =
    activeJob?.status === 'Queued' || activeJob?.status === 'Processing';

  const loadScheduleStatus = useCallback(async (year = statusYear) => {
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
  }, [statusYear]);

  const loadJobs = useCallback(async (syncActiveJob = true) => {
    setLoadingJobs(true);
    try {
      const response = await axiosInstance.get<DataImportJob[]>(
        `${config.apiRoute}/jobs`
      );
      const nextJobs = response.data ?? [];
      setJobs(nextJobs);
      if (!syncActiveJob) {
        return;
      }

      setActiveJob((currentJob) => {
        if (
          currentJob?.status === 'Queued' ||
          currentJob?.status === 'Processing'
        ) {
          return currentJob;
        }

        const pendingJob = nextJobs.find(
          (job) => job.status === 'Queued' || job.status === 'Processing'
        );

        return pendingJob ?? currentJob ?? nextJobs[0] ?? null;
      });
    } catch {
      message.error('Could not load import jobs.');
    } finally {
      setLoadingJobs(false);
    }
  }, []);

  const resetImportProgress = useCallback(() => {
    setActiveJob(null);
    loadJobs(false);
  }, [loadJobs]);

  const loadJob = useCallback(async (jobId: string) => {
    const response = await axiosInstance.get<DataImportJob>(
      `${config.apiRoute}/jobs/${jobId}`
    );
    return response.data;
  }, []);

  const handleSave = async (values: FormValues) => {
    setSaving(true);
    try {
      const response = await axiosInstance.post<DataImportJob>(
        `${config.apiRoute}/jobs`,
        {
          licenceType: values.licenceType,
          startDate: values.dateRange[0]
            .startOf('day')
            .format('YYYY-MM-DDTHH:mm:ss'),
          endDate: values.dateRange[1]
            .startOf('day')
            .format('YYYY-MM-DDTHH:mm:ss'),
        }
      );
      setActiveJob(response.data);
      message.success('Data import queued.');
      loadJobs();
    } catch {
      message.error('Could not queue the data import.');
    } finally {
      setSaving(false);
    }
  };

  useEffect(() => {
    loadScheduleStatus(statusYear);
    loadJobs();
  }, [loadJobs, loadScheduleStatus, statusYear]);

  useEffect(() => {
    if (pollingTimerRef.current) {
      clearInterval(pollingTimerRef.current);
      pollingTimerRef.current = null;
    }

    if (!activeJob || !isActiveJobPending) {
      return;
    }

    pollingTimerRef.current = setInterval(async () => {
      try {
        const nextJob = await loadJob(activeJob.id);
        setActiveJob(nextJob);

        if (nextJob.status === 'Completed') {
          message.success('Data import completed.');
          loadJobs();
          loadScheduleStatus(statusYear);
        } else if (nextJob.status === 'Failed') {
          message.error('Data import failed.');
          loadJobs();
        }
      } catch {
        message.error('Could not load import progress.');
      }
    }, 5000);

    return () => {
      if (pollingTimerRef.current) {
        clearInterval(pollingTimerRef.current);
        pollingTimerRef.current = null;
      }
    };
  }, [
    activeJob,
    isActiveJobPending,
    loadJob,
    loadJobs,
    loadScheduleStatus,
    statusYear,
  ]);

  const jobColumns = useMemo(
    () => [
      {
        key: 'dateRange',
        title: 'Date Range',
        render: (_: unknown, job: DataImportJob) =>
          `${dayjs(job.startDate).format('YYYY-MM-DD')} to ${dayjs(
            job.endDate
          ).format('YYYY-MM-DD')}`,
      },
      {
        key: 'licenceType',
        dataIndex: 'licenceType',
        title: 'Licence Type',
        render: (value: string) => licenceTypeLabelMap.get(value) ?? value,
      },
      {
        key: 'status',
        dataIndex: 'status',
        title: 'Status',
        render: (status: DataImportJobStatus, job: DataImportJob) => (
          <Space direction="vertical" size={4} style={{ minWidth: 180 }}>
            <Tag color={jobStatusColor[status]} style={{ width: 'fit-content' }}>
              {status}
            </Tag>
            <Progress
              percent={job.progressPercent}
              size="small"
              status={status === 'Failed' ? 'exception' : undefined}
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {`${job.processedDays} / ${job.totalDays} days`}
            </Typography.Text>
          </Space>
        ),
      },
      {
        key: 'totalRows',
        dataIndex: 'totalRows',
        title: 'Saved Rows',
        align: 'right' as const,
        render: (value: number) => value.toLocaleString(),
      },
      {
        key: 'requestedBy',
        dataIndex: 'requestedBy',
        title: 'Requested By',
        render: (value: string | null) => value ?? '-',
      },
      {
        key: 'createdAtUtc',
        dataIndex: 'createdAtUtc',
        title: 'Created',
        render: formatDateTime,
      },
      {
        key: 'completedAtUtc',
        dataIndex: 'completedAtUtc',
        title: 'Completed',
        render: formatDateTime,
      },
      {
        key: 'errorMessage',
        dataIndex: 'errorMessage',
        title: 'Error',
        render: (value: string | null) => value ?? '-',
      },
    ],
    []
  );

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
      <Tabs
        defaultActiveKey="import"
        items={[
          {
            key: 'import',
            label: 'Data Import',
            children: (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Card>
                  <Form
                    form={form}
                    layout="vertical"
                    initialValues={{
                      licenceType: 'All',
                      dateRange: [dayjs().subtract(30, 'day'), dayjs()],
                    }}
                    onFinish={handleSave}
                  >
                    <div style={importFormGridStyle}>
                      <Form.Item
                        label="Licence Type"
                        name="licenceType"
                        rules={[
                          {
                            required: true,
                            message: 'Licence type is required',
                          },
                        ]}
                        style={importFormItemStyle}
                      >
                        <Select
                          style={{ width: '100%' }}
                          options={licenceTypeOptions}
                        />
                      </Form.Item>
                      <Form.Item
                        label="Date Range"
                        name="dateRange"
                        rules={[
                          {
                            required: true,
                            message: 'Date range is required',
                          },
                        ]}
                        style={importFormItemStyle}
                      >
                        <RangePicker
                          allowClear={false}
                          format="YYYY-MM-DD"
                          inputReadOnly
                          placement="bottomLeft"
                          style={{ width: '100%' }}
                        />
                      </Form.Item>
                      <Form.Item style={importFormItemStyle}>
                        <Button
                          type="primary"
                          htmlType="submit"
                          icon={<SaveOutlined />}
                          loading={saving}
                        >
                          Start Import
                        </Button>
                      </Form.Item>
                    </div>
                  </Form>
                </Card>

                <Card>
                  <Space direction="vertical" size={14} style={{ width: '100%' }}>
                    <Space
                      align="center"
                      style={{ justifyContent: 'space-between', width: '100%' }}
                      wrap
                    >
                      <Typography.Text strong style={{ fontSize: 18 }}>
                        Import Progress
                      </Typography.Text>
                      <Space>
                        <Button
                          icon={<ReloadOutlined />}
                          loading={loadingJobs}
                          onClick={resetImportProgress}
                        >
                          Refresh
                        </Button>
                        <Tag
                          color={
                            activeJob
                              ? jobStatusColor[activeJob.status]
                              : 'default'
                          }
                        >
                          {activeJob ? activeJob.status : 'No Job'}
                        </Tag>
                      </Space>
                    </Space>

                    <AnimatedImportProgress
                      percent={activeJob?.progressPercent ?? 0}
                      status={activeJob?.status}
                    />

                    {activeJob ? (
                      <>
                        <Space size={24} wrap>
                          <Typography.Text>
                            {`Date Range: ${dayjs(activeJob.startDate).format(
                              'YYYY-MM-DD'
                            )} to ${dayjs(activeJob.endDate).format(
                              'YYYY-MM-DD'
                            )}`}
                          </Typography.Text>
                          <Typography.Text>{`Licence Type: ${
                            licenceTypeLabelMap.get(activeJob.licenceType) ??
                            activeJob.licenceType
                          }`}</Typography.Text>
                          <Typography.Text>{`Progress: ${activeJob.processedDays} / ${activeJob.totalDays} days`}</Typography.Text>
                          <Typography.Text>{`Saved Rows: ${activeJob.totalRows.toLocaleString()}`}</Typography.Text>
                        </Space>
                        {activeJob.errorMessage && (
                          <Typography.Text type="danger">
                            {activeJob.errorMessage}
                          </Typography.Text>
                        )}
                      </>
                    ) : (
                      <Typography.Text type="secondary">
                        No import job has been started yet.
                      </Typography.Text>
                    )}
                  </Space>
                </Card>

                <Card>
                  <Space direction="vertical" size={12} style={{ width: '100%' }}>
                    <Space
                      align="center"
                      style={{ justifyContent: 'space-between', width: '100%' }}
                      wrap
                    >
                      <Typography.Text strong style={{ fontSize: 18 }}>
                        Recent Import Jobs
                      </Typography.Text>
                      <Button
                        icon={<ReloadOutlined />}
                        loading={loadingJobs}
                        onClick={() => loadJobs()}
                      >
                        Refresh
                      </Button>
                    </Space>
                    <Table<DataImportJob>
                      rowKey="id"
                      size="small"
                      loading={loadingJobs}
                      pagination={{ pageSize: 10, showSizeChanger: true }}
                      dataSource={jobs}
                      columns={jobColumns}
                    />
                  </Space>
                </Card>
              </Space>
            ),
          },
          {
            key: 'schedule',
            label: 'Schedule Checklist',
            children: (
              <Card>
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
                    <Typography.Text
                      strong
                      style={{ fontSize: 18, lineHeight: '32px' }}
                    >
                      Schedule Checklist
                    </Typography.Text>
                    <Space align="end" wrap>
                      <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">
                          Year
                        </Typography.Text>
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
                        <Space
                          size={8}
                          style={{ alignItems: 'center', height: 32 }}
                        >
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
                        gridTemplateColumns:
                          'repeat(auto-fit, minmax(260px, 1fr))',
                      }}
                    >
                      {Array.from({ length: 12 }, (_, index) =>
                        renderMonth(index)
                      )}
                    </div>
                  )}
                </Space>
              </Card>
            ),
          },
        ]}
      />
    </>
  );
};

export default ImportLicenceDataImport;
