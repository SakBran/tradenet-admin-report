import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const ImportLicenceByHSCodeReport = () => (
  <GenericReportPage config={reportConfigs.ImportLicenceByHSCodeReport} />
);

export default ImportLicenceByHSCodeReport;
