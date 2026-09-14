import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const EICCCertificateReport = () => (
  <GenericReportPage config={reportConfigs.EICCCertificateReport} />
);

export default EICCCertificateReport;
