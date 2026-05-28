import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const ExportLicenceByHSCodeReport = () => (
  <GenericReportPage config={reportConfigs.ExportLicenceByHSCodeReport} />
);

export default ExportLicenceByHSCodeReport;
