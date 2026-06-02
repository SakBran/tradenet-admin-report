import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const SaleCenterDetailReport = () => (
  <GenericReportPage config={reportConfigs.SaleCenterDetailReport} />
);

export default SaleCenterDetailReport;
