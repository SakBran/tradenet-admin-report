import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const WholeSaleSummaryReport = () => (
  <GenericReportPage config={reportConfigs.WholeSaleSummaryReport} />
);

export default WholeSaleSummaryReport;
