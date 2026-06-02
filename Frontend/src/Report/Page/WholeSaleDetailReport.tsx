import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const WholeSaleDetailReport = () => (
  <GenericReportPage config={reportConfigs.WholeSaleDetailReport} />
);

export default WholeSaleDetailReport;
