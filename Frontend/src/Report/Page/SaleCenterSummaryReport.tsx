import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const SaleCenterSummaryReport = () => (
  <GenericReportPage config={reportConfigs.SaleCenterSummaryReport} />
);

export default SaleCenterSummaryReport;
