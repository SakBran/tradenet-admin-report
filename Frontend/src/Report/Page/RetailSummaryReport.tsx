import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const RetailSummaryReport = () => (
  <GenericReportPage config={reportConfigs.RetailSummaryReport} />
);

export default RetailSummaryReport;
