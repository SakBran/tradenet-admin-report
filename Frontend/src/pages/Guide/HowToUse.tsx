import { Card, Col, Flex, Row, Space, theme, Typography } from 'antd';
import {
  AppstoreOutlined,
  BulbOutlined,
  CheckCircleFilled,
  FileExcelOutlined,
  FilterOutlined,
  ReloadOutlined,
  SearchOutlined,
  TableOutlined,
} from '@ant-design/icons';
import { ReactNode } from 'react';

const { Title, Text, Paragraph } = Typography;

type Step = {
  icon: ReactNode;
  title: string;
  description: string;
};

type Feature = {
  icon: ReactNode;
  title: string;
  description: string;
};

const steps: Step[] = [
  {
    icon: <AppstoreOutlined />,
    title: 'Open a Report',
    description:
      'Use the left sidebar to pick a category (Member, Licence, Permit, Border, Payment…) and choose the report you need.',
  },
  {
    icon: <FilterOutlined />,
    title: 'Set the Filters',
    description:
      'Choose a date range and any other options such as Apply Type, then click Filter to load the data.',
  },
  {
    icon: <TableOutlined />,
    title: 'Review the Results',
    description:
      'Browse the report table. Use the page size and pager at the bottom to move through large result sets.',
  },
  {
    icon: <FileExcelOutlined />,
    title: 'Export to Excel',
    description:
      'Click the green Excel button to download the current report as a spreadsheet for sharing or printing.',
  },
];

const features: Feature[] = [
  {
    icon: <AppstoreOutlined />,
    title: 'Reports by Category',
    description:
      'Every report is grouped by type in the sidebar so you can find what you need at a glance.',
  },
  {
    icon: <SearchOutlined />,
    title: 'Filter & Search',
    description:
      'Narrow results by date range and report-specific options to focus on exactly what matters.',
  },
  {
    icon: <FileExcelOutlined />,
    title: 'One-click Excel',
    description:
      'Export any report to Excel with a single click — formatted and ready to use.',
  },
];

const tips = [
  'Always click Filter after changing the date range to refresh the table.',
  'Use the Reset button to quickly return filters to their default values.',
  'Increase the page size at the bottom of a table to see more rows at once.',
  'Collapse the sidebar with the menu button to give the report more space.',
];

export const HowToUsePage = () => {
  const {
    token: { colorPrimary, colorPrimaryBg, colorFillTertiary, borderRadiusLG },
  } = theme.useToken();

  return (
    <Flex vertical gap="large">
      {/* Hero */}
      <Card
        styles={{ body: { padding: 0 } }}
        style={{
          overflow: 'hidden',
          border: 'none',
          borderRadius: borderRadiusLG,
        }}
      >
        <Flex
          align="center"
          gap="large"
          wrap="wrap"
          style={{
            background: `linear-gradient(135deg, ${colorPrimary} 0%, #1565c0 100%)`,
            padding: '32px 36px',
          }}
        >
          <div
            style={{
              background: '#ffffff',
              borderRadius: '50%',
              padding: 16,
              boxShadow: '0 6px 18px rgba(0,0,0,0.18)',
              lineHeight: 0,
              flex: '0 0 auto',
            }}
          >
            <img
              src="/moc-logo.png"
              alt="Ministry of Commerce logo"
              height={84}
              width={84}
              style={{ display: 'block' }}
            />
          </div>
          <Flex vertical gap={6} style={{ flex: '1 1 280px' }}>
            <Title level={2} style={{ color: '#fff', margin: 0 }}>
              Welcome to T2.0 Report
            </Title>
            <Text style={{ color: 'rgba(255,255,255,0.92)', fontSize: 16 }}>
              The Ministry of Commerce reporting and administration portal.
              Here&apos;s how to get the most out of it in just a few steps.
            </Text>
          </Flex>
        </Flex>
      </Card>

      {/* Quick start steps */}
      <div>
        <Title level={4} style={{ marginBottom: 4 }}>
          Get started in 4 steps
        </Title>
        <Text type="secondary">
          From opening a report to exporting your results.
        </Text>

        <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
          {steps.map((step, index) => (
            <Col xs={24} sm={12} lg={6} key={step.title}>
              <Card
                hoverable
                style={{ height: '100%', borderRadius: borderRadiusLG }}
              >
                <Flex vertical gap={12}>
                  <Flex align="center" justify="space-between">
                    <div
                      style={{
                        width: 48,
                        height: 48,
                        borderRadius: '50%',
                        background: colorPrimaryBg,
                        color: colorPrimary,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: 22,
                      }}
                    >
                      {step.icon}
                    </div>
                    <Text
                      style={{
                        fontSize: 40,
                        fontWeight: 700,
                        lineHeight: 1,
                        color: colorFillTertiary,
                      }}
                    >
                      {index + 1}
                    </Text>
                  </Flex>
                  <Title level={5} style={{ margin: 0 }}>
                    {step.title}
                  </Title>
                  <Paragraph type="secondary" style={{ margin: 0 }}>
                    {step.description}
                  </Paragraph>
                </Flex>
              </Card>
            </Col>
          ))}
        </Row>
      </div>

      {/* Feature highlights */}
      <div>
        <Title level={4} style={{ marginBottom: 4 }}>
          What you can do
        </Title>
        <Text type="secondary">Key tools available on every report.</Text>

        <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
          {features.map((feature) => (
            <Col xs={24} md={8} key={feature.title}>
              <Card style={{ height: '100%', borderRadius: borderRadiusLG }}>
                <Flex gap="middle" align="flex-start">
                  <div
                    style={{
                      width: 44,
                      height: 44,
                      borderRadius: 10,
                      background: colorPrimaryBg,
                      color: colorPrimary,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: 20,
                      flex: '0 0 auto',
                    }}
                  >
                    {feature.icon}
                  </div>
                  <Flex vertical gap={4}>
                    <Title level={5} style={{ margin: 0 }}>
                      {feature.title}
                    </Title>
                    <Text type="secondary">{feature.description}</Text>
                  </Flex>
                </Flex>
              </Card>
            </Col>
          ))}
        </Row>
      </div>

      {/* Tips */}
      <Card
        style={{ borderRadius: borderRadiusLG, background: colorPrimaryBg, border: 'none' }}
      >
        <Flex align="center" gap="small" style={{ marginBottom: 12 }}>
          <BulbOutlined style={{ fontSize: 20, color: colorPrimary }} />
          <Title level={5} style={{ margin: 0 }}>
            Handy tips
          </Title>
        </Flex>
        <Space direction="vertical" size={10} style={{ width: '100%' }}>
          {tips.map((tip) => (
            <Flex key={tip} align="flex-start" gap="small">
              <CheckCircleFilled style={{ color: colorPrimary, marginTop: 4 }} />
              <Text>{tip}</Text>
            </Flex>
          ))}
        </Space>
        <Flex align="center" gap={6} style={{ marginTop: 16 }}>
          <ReloadOutlined style={{ color: colorPrimary }} />
          <Text type="secondary">
            Tip: the Reset button on any report restores the default filters.
          </Text>
        </Flex>
      </Card>
    </Flex>
  );
};

export default HowToUsePage;
