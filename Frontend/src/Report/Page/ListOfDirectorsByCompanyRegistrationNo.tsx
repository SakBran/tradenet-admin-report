import { useCallback, useState } from 'react';
import { Button, Card, Empty, Form, Input, Space, Spin, message } from 'antd';
import { PrinterOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import axiosInstance from '../../services/AxiosInstance';
import { PageHeader } from '../../components';

const API_ROUTE = 'ListOfDirectorsByCompanyRegistrationNo/Detail';

interface AddressParts {
  unitLevel?: string | null;
  streetNumberStreetName?: string | null;
  quarterCityTownship?: string | null;
  state?: string | null;
  country?: string | null;
  postalCode?: string | null;
}

interface CompanyInfo extends AddressParts {
  companyRegistrationNo: string;
  companyName: string;
  companyRegistrationDate: string;
  endDate: string;
  businessType: string;
  lineofBusiness?: string | null;
}

interface Director extends AddressParts {
  directorName?: string | null;
  directorNRC?: string | null;
  directorPosition?: string | null;
}

interface DetailResult {
  companyRegistrationNo: string;
  company?: CompanyInfo | null;
  directors: Director[];
}

const formatDate = (value?: string | null) => {
  if (!value) {
    return '';
  }
  const parsed = dayjs(value);
  return parsed.isValid() ? parsed.format('DD/MM/YYYY') : '';
};

const buildAddress = (parts: AddressParts) => {
  const stateLine = [parts.state, parts.postalCode]
    .map((part) => part?.toString().trim())
    .filter(Boolean)
    .join(' ');

  return [
    parts.unitLevel,
    parts.streetNumberStreetName,
    parts.quarterCityTownship,
    stateLine,
    parts.country,
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
};
const tableStyle: React.CSSProperties = {
  width: '100%',
  borderCollapse: 'collapse',
  tableLayout: 'fixed',
  marginBottom: 12,
};

const ListOfDirectorsByCompanyRegistrationNo = () => {
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
        <PageHeader title="List of Directors By Company Registration No" />

        <Card style={{ marginBottom: 16 }}>
          <Form form={form} layout="inline" onFinish={handleSearch}>
            <Form.Item
              name="companyRegistrationNo"
              label="Company Registration No"
              rules={[
                {
                  required: true,
                  message: 'Company Registration No is required.',
                },
              ]}
            >
              <Input
                placeholder="e.g. 143892891"
                allowClear
                style={{ width: 240 }}
              />
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
              <div style={{ fontWeight: 700 }}>Directorate of Trade</div>
              <div style={{ fontWeight: 700 }}>
                List of Directors ({data?.companyRegistrationNo})
              </div>
            </div>

            {/* Company info */}
            <table style={tableStyle}>
              <thead>
                <tr>
                  <th style={headerCell}>Company Registration No</th>
                  <th style={headerCell}>Company Name</th>
                  <th style={headerCell}>Company Address</th>
                  <th style={headerCell}>Company Registration Date</th>
                  <th style={headerCell}>Valid Date</th>
                  <th style={headerCell}>Business Type</th>
                  <th style={headerCell}>Line of Business</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td style={cell}>{company.companyRegistrationNo}</td>
                  <td style={cell}>{company.companyName}</td>
                  <td style={cell}>{buildAddress(company)}</td>
                  <td style={cell}>
                    {formatDate(company.companyRegistrationDate)}
                  </td>
                  <td style={cell}>{formatDate(company.endDate)}</td>
                  <td style={cell}>{company.businessType}</td>
                  <td style={cell}>{company.lineofBusiness}</td>
                </tr>
              </tbody>
            </table>

            {/* List of Directors */}
            <table style={tableStyle}>
              <thead>
                <tr>
                  <th style={sectionBar} colSpan={5}>
                    List of Directors
                  </th>
                </tr>
                <tr>
                  <th style={{ ...headerCell, width: 50 }}>No</th>
                  <th style={headerCell}>Name</th>
                  <th style={headerCell}>NRC No</th>
                  <th style={headerCell}>Position</th>
                  <th style={headerCell}>Address</th>
                </tr>
              </thead>
              <tbody>
                {data && data.directors.length > 0 ? (
                  data.directors.map((director, index) => (
                    <tr key={`director-${index}`}>
                      <td style={{ ...cell, textAlign: 'center' }}>
                        {index + 1}
                      </td>
                      <td style={cell}>{director.directorName}</td>
                      <td style={cell}>{director.directorNRC}</td>
                      <td style={cell}>{director.directorPosition}</td>
                      <td style={cell}>{buildAddress(director)}</td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td
                      style={{ ...cell, textAlign: 'center', color: '#999' }}
                      colSpan={5}
                    >
                      -
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
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

export default ListOfDirectorsByCompanyRegistrationNo;
