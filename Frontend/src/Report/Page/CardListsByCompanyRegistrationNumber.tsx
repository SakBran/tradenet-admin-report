import GenericReportPage from './GenericReportPage';
import { reportConfigs } from '../config/reportConfigs';

const CardListsByCompanyRegistrationNumber = () => (
  <GenericReportPage config={reportConfigs.CardListsByCompanyRegistrationNumber} />
);

export default CardListsByCompanyRegistrationNumber;
