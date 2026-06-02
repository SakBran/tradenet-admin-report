import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const AlcoholicBeveragesImportationSummaryReport = () => (
  <GenericReportPage
    config={reportConfigs.AlcoholicBeveragesImportationSummaryReport}
  />
);

export default AlcoholicBeveragesImportationSummaryReport;
