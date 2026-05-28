import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const ExportPermitByHSCodeReport = () => (
  <GenericReportPage config={reportConfigs.ExportPermitByHSCodeReport} />
);

export default ExportPermitByHSCodeReport;
