import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const AccountSummaryReport = () => (
  <GenericReportPage config={reportConfigs.AccountSummaryReport} />
);

export default AccountSummaryReport;
