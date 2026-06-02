import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const BusinessServiceAgencySummaryReport = () => (
  <GenericReportPage config={reportConfigs.BusinessServiceAgencySummaryReport} />
);

export default BusinessServiceAgencySummaryReport;
