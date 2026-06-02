import { useCallback, useState } from 'react';
import { Button, Card, Empty, Form, Input, Space, Spin, message } from 'antd';
import { PrinterOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { PageHeader } from '../../components';

const API_ROUTE = 'CardListsByCompanyRegistrationNumber/Detail';

interface CompanyInfo {
  micpermitNo?: string | null;
  companyRegistrationNo: string;
  companyName: string;
  companyRegistrationDate: string;
  endDate: string;
  businessType: string;
  lineofBusiness?: string | null;
  unitLevel?: string | null;
  streetNumberStreetName?: string | null;
  quarterCityTownship?: string | null;
  state?: string | null;
  country?: string | null;
  postalCode?: string | null;
}

interface Director {
  directorName?: string | null;
  directorNRC?: string | null;
}

interface RelatedCard {
  issuedDate: string;
  endDate: string;
}

interface WholeSaleRetailCard {
  wholeSaleRetailNo: string;
  wholeSaleRetailIssuedDate: string;
  wholeSaleRetailEndDate: string;
}

interface AlcoholicCard extends RelatedCard {
  wineImportationNo: string;
}

interface BusinessServiceAgencyCard extends RelatedCard {
  businessServiceAgencyNo: string;
}

interface ReExportCard extends RelatedCard {
  reExportNo: string;
}

interface SaleCenterCard {
  saleCenterNo: string;
  saleCenterIssuedDate: string;
  saleCenterEndDate: string;
}

interface ShowRoomCard extends RelatedCard {
  showRoomNo: string;
}

interface DutyFreeShopCard extends RelatedCard {
  dutyFreeShopNo: string;
}

interface DetailResult {
  companyRegistrationNo: string;
  company?: CompanyInfo | null;
  permitBusinesses: string[];
  directors: Director[];
  wholeSaleRetail: WholeSaleRetailCard[];
  alcoholicBeverages: AlcoholicCard[];
  businessServiceAgency: BusinessServiceAgencyCard[];
  reExport: ReExportCard[];
  saleCenter: SaleCenterCard[];
  showRoom: ShowRoomCard[];
  dutyFreeShop: DutyFreeShopCard[];
}

const formatDate = (value?: string | null) => {
  if (!value) {
    return '';
  }
  const parsed = dayjs(value);
  return parsed.isValid() ? parsed.format('DD/MM/YYYY') : '';
};

const buildAddress = (company: CompanyInfo) => {
  const stateLine = [company.state, company.postalCode]
    .map((part) => part?.toString().trim())
    .filter(Boolean)
    .join(' ');

  return [
    company.unitLevel,
    company.streetNumberStreetName,
    company.quarterCityTownship,
    stateLine,
    company.country,
  ]
    .map((part) => part?.toString().trim())
    .filter(Boolean)
    .join(', ');
};

// --- presentational styles (kept inline so the report prints faithfully) ---
const cell: React.CSSProperties = {
  border: '1px solid #000',
  padding: '6px 8px',
  fontSize: 12,
  verticalAlign: 'top',
};
const headerCell: React.CSSProperties = {
  ...cell,
  backgroundColor: '#d9d9d9',
  fontWeight: 600,
  textAlign: 'center',
};
const sectionBar: React.CSSProperties = {
  ...cell,
  backgroundColor: '#d9d9d9',
  fontWeight: 600,
  textAlign: 'center',
};
const tableStyle: React.CSSProperties = {
  width: '100%',
  borderCollapse: 'collapse',
  tableLayout: 'fixed',
  marginBottom: 12,
};

interface RelatedSection {
  title: string;
  noHeader: string;
  issuedHeader: string;
  validHeader: string;
  rows: Array<{ no: string; issued: string; valid: string }>;
}

const RelatedCardTable = ({ section }: { section: RelatedSection }) => (
  <table style={tableStyle}>
    <thead>
      <tr>
        <th style={headerCell}>{section.noHeader}</th>
        <th style={headerCell}>{section.issuedHeader}</th>
        <th style={headerCell}>{section.validHeader}</th>
      </tr>
    </thead>
    <tbody>
      {section.rows.length === 0 ? (
        <tr>
          <td style={{ ...cell, textAlign: 'center', color: '#999' }} colSpan={3}>
            -
          </td>
        </tr>
      ) : (
        section.rows.map((row, index) => (
          <tr key={`${section.title}-${index}`}>
            <td style={cell}>{row.no}</td>
            <td style={cell}>{row.issued}</td>
            <td style={cell}>{row.valid}</td>
          </tr>
        ))
      )}
    </tbody>
  </table>
);

const CardListsByCompanyRegistrationNumber = () => {
  const [form] = Form.useForm<{ companyRegistrationNo: string }>();
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<DetailResult | null>(null);
  const [searched, setSearched] = useState(false);

  const handleSearch = useCallback(
    async (values: { companyRegistrationNo: string }) => {
      const registrationNo = values.companyRegistrationNo?.trim();
      if (!registrationNo) {
        message.warning('Please enter a Company Registration No.');
        return;
      }

      setLoading(true);
      try {
        const response = await axiosInstance.post<DetailResult>(API_ROUTE, {
          companyRegistrationNo: registrationNo,
        });
        setData(response.data);
        setSearched(true);
      } catch (error) {
        message.error('Failed to load the report. Please try again.');
      } finally {
        setLoading(false);
      }
    },
    []
  );

  const handlePrint = useCallback(() => {
    window.print();
  }, []);

  const company = data?.company ?? null;

  const relatedSections: RelatedSection[] = data
    ? [
        {
          title: 'WholeSaleRetail',
          noHeader: 'Whole Sale Retail No',
          issuedHeader: 'WSRIssued Date',
          validHeader: 'WSRValid Date',
          rows: data.wholeSaleRetail.map((card) => ({
            no: card.wholeSaleRetailNo,
            issued: formatDate(card.wholeSaleRetailIssuedDate),
            valid: formatDate(card.wholeSaleRetailEndDate),
          })),
        },
        {
          title: 'DutyFreeShop',
          noHeader: 'Duty Free Shop No',
          issuedHeader: 'DFIssued Date',
          validHeader: 'DFValid Date',
          rows: data.dutyFreeShop.map((card) => ({
            no: card.dutyFreeShopNo,
            issued: formatDate(card.issuedDate),
            valid: formatDate(card.endDate),
          })),
        },
        {
          title: 'AlcoholicBeverages',
          noHeader: 'Alcoholic Beverages Importation No',
          issuedHeader: 'ABIIssued Date',
          validHeader: 'ABIValid Date',
          rows: data.alcoholicBeverages.map((card) => ({
            no: card.wineImportationNo,
            issued: formatDate(card.issuedDate),
            valid: formatDate(card.endDate),
          })),
        },
        {
          title: 'ReExport',
          noHeader: 'Re Export No',
          issuedHeader: 'REIssued Date',
          validHeader: 'REValid Date',
          rows: data.reExport.map((card) => ({
            no: card.reExportNo,
            issued: formatDate(card.issuedDate),
            valid: formatDate(card.endDate),
          })),
        },
        {
          title: 'BusinessServiceAgency',
          noHeader: 'BSANo',
          issuedHeader: 'BSAIssued Date',
          validHeader: 'BSAValid Date',
          rows: data.businessServiceAgency.map((card) => ({
            no: card.businessServiceAgencyNo,
            issued: formatDate(card.issuedDate),
            valid: formatDate(card.endDate),
          })),
        },
        {
          title: 'SaleCenter',
          noHeader: 'Sale Center No',
          issuedHeader: 'SCIssued Date',
          validHeader: 'SCValid Date',
          rows: data.saleCenter.map((card) => ({
            no: card.saleCenterNo,
            issued: formatDate(card.saleCenterIssuedDate),
            valid: formatDate(card.saleCenterEndDate),
          })),
        },
        {
          title: 'ShowRoom',
          noHeader: 'Show Room No',
          issuedHeader: 'SRIssued Date',
          validHeader: 'SRValid Date',
          rows: data.showRoom.map((card) => ({
            no: card.showRoomNo,
            issued: formatDate(card.issuedDate),
            valid: formatDate(card.endDate),
          })),
        },
      ]
    : [];

  return (
    <div>
      <style>
        {`@media print {
            body * { visibility: hidden; }
            .pathaka-print-area, .pathaka-print-area * { visibility: visible; }
            .pathaka-print-area { position: absolute; left: 0; top: 0; width: 100%; }
            .pathaka-no-print { display: none !important; }
          }`}
      </style>

      <div className="pathaka-no-print">
        <PageHeader title="Card Lists By Company Registration Number" />

        <Card style={{ marginBottom: 16 }}>
          <Form form={form} layout="inline" onFinish={handleSearch}>
            <Form.Item
              name="companyRegistrationNo"
              label="Company Registration No"
              rules={[
                { required: true, message: 'Company Registration No is required.' },
              ]}
            >
              <Input placeholder="e.g. 102075447" allowClear style={{ width: 240 }} />
            </Form.Item>
            <Form.Item>
              <Space>
                <Button
                  type="primary"
                  htmlType="submit"
                  icon={<SearchOutlined />}
                  loading={loading}
                >
                  Search
                </Button>
                <Button
                  icon={<PrinterOutlined />}
                  onClick={handlePrint}
                  disabled={!company}
                >
                  Print
                </Button>
              </Space>
            </Form.Item>
          </Form>
        </Card>
      </div>

      <Spin spinning={loading}>
        {company ? (
          <Card className="pathaka-print-area">
            {/* Report title */}
            <div style={{ textAlign: 'center', marginBottom: 12 }}>
              <div style={{ fontWeight: 700 }}>Ministry of Commerce</div>
              <div style={{ fontWeight: 700 }}>Applied Card Lists</div>
            </div>

            {/* Company header */}
            <table style={tableStyle}>
              <thead>
                <tr>
                  <th style={headerCell}>Company Registration No</th>
                  <th style={headerCell}>Company Name</th>
                  <th style={headerCell}>Company Address</th>
                  <th style={headerCell}>Start Date</th>
                  <th style={headerCell}>Valid Date</th>
                  <th style={headerCell}>Business Type</th>
                  <th style={headerCell}>Line Of Business</th>
                  <th style={headerCell}>MIC Permit Number</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td style={cell}>{company.companyRegistrationNo}</td>
                  <td style={cell}>{company.companyName}</td>
                  <td style={cell}>{buildAddress(company)}</td>
                  <td style={cell}>{formatDate(company.companyRegistrationDate)}</td>
                  <td style={cell}>{formatDate(company.endDate)}</td>
                  <td style={cell}>{company.businessType}</td>
                  <td style={cell}>{company.lineofBusiness}</td>
                  <td style={cell}>{company.micpermitNo}</td>
                </tr>
              </tbody>
            </table>

            {/* Permit Business */}
            <table style={tableStyle}>
              <thead>
                <tr>
                  <th style={sectionBar}>Permit Business</th>
                </tr>
              </thead>
              <tbody>
                {data && data.permitBusinesses.length > 0 ? (
                  data.permitBusinesses.map((description, index) => (
                    <tr key={`permit-${index}`}>
                      <td style={cell}>{description}</td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td style={{ ...cell, textAlign: 'center', color: '#999' }}>-</td>
                  </tr>
                )}
              </tbody>
            </table>

            {/* Director Info */}
            <table style={tableStyle}>
              <thead>
                <tr>
                  <th style={headerCell}>Director Name</th>
                  <th style={headerCell}>NRC Number</th>
                </tr>
              </thead>
              <tbody>
                {data && data.directors.length > 0 ? (
                  data.directors.map((director, index) => (
                    <tr key={`director-${index}`}>
                      <td style={{ ...cell, textAlign: 'center' }}>
                        {director.directorName}
                      </td>
                      <td style={{ ...cell, textAlign: 'center' }}>
                        {director.directorNRC}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td style={{ ...cell, textAlign: 'center', color: '#999' }} colSpan={2}>
                      -
                    </td>
                  </tr>
                )}
              </tbody>
            </table>

            {/* Related cards */}
            {relatedSections.map((section) => (
              <RelatedCardTable key={section.title} section={section} />
            ))}
          </Card>
        ) : (
          searched &&
          !loading && (
            <Card>
              <Empty description="No company found for this Company Registration No." />
            </Card>
          )
        )}
      </Spin>
    </div>
  );
};

export default CardListsByCompanyRegistrationNumber;
