import {
  Button,
  Checkbox,
  Col,
  Flex,
  Form,
  Input,
  message,
  Row,
  theme,
  Typography,
} from 'antd';
import { useMediaQuery } from 'react-responsive';
import { PATH_AUTH, PATH_DASHBOARD } from '../../constants';
import { useNavigate } from 'react-router-dom';
import { useContext, useState } from 'react';
import AuthContext from '../../context/AuthContext';
import { AnyObject } from '../../types/AnyObject';
import { Link } from 'react-router-dom';

const { Title, Text } = Typography;

type FieldType = {
  email?: string;
  password?: string;
  remember?: boolean;
};

export const SignInPage = () => {
  const {
    token: { colorPrimary },
  } = theme.useToken();
  const isMobile = useMediaQuery({ maxWidth: 769 });
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const auth = useContext(AuthContext);

  const onFinish = (values: AnyObject) => {
    console.log('Success:', values);
    setLoading(true);
    const login = async () => {
      const response = await auth?.login(values['email'], values['password']);
      if (response) {
        setLoading(false);
        message.open({
          type: 'success',
          content: 'Login successful',
        });
        setTimeout(() => {
          navigate(PATH_DASHBOARD.default);
        }, 450);
      } else {
        setLoading(false);
        message.open({
          type: 'error',
          content: 'Login fail',
        });
      }
    };
    login();
  };

  const onFinishFailed = (errorInfo: unknown) => {
    console.log('Failed:', errorInfo);
  };

  return (
    <Row style={{ minHeight: isMobile ? 'auto' : '100vh', overflow: 'hidden' }}>
      <Col xs={24} lg={12}>
        <Flex
          vertical
          align="center"
          justify="center"
          className="text-center"
          style={{ background: colorPrimary, height: '100%', padding: '1rem' }}
        >
          <div
            style={{
              background: '#ffffff',
              borderRadius: '50%',
              padding: 20,
              marginBottom: 16,
              boxShadow: '0 6px 18px rgba(0, 0, 0, 0.18)',
              lineHeight: 0,
            }}
          >
            <img
              src="/moc-logo.png"
              alt="Ministry of Commerce logo"
              height={108}
              width={108}
              style={{ display: 'block' }}
            />
          </div>
          <Title level={2} className="text-white">
            Welcome back to T2.0 Report
          </Title>
          <Text className="text-white" style={{ fontSize: 18 }}>
            Ministry of Commerce — Tradenet 2.0 reporting and administration
            portal.
          </Text>
        </Flex>
      </Col>
      <Col xs={24} lg={12}>
        <Flex
          vertical
          align={isMobile ? 'center' : 'flex-start'}
          justify="center"
          gap="middle"
          style={{ height: '100%', padding: '2rem' }}
        >
          <Title className="m-0">Login</Title>
          <Text type="secondary">
            Sign in with your Ministry of Commerce account to continue.
          </Text>
          <Form
            name="sign-up-form"
            layout="vertical"
            labelCol={{ span: 24 }}
            wrapperCol={{ span: 24 }}
            initialValues={{
              email: 'demo@email.com',
              password: 'demo123',
              remember: true,
            }}
            onFinish={onFinish}
            onFinishFailed={onFinishFailed}
            autoComplete="off"
            requiredMark={false}
          >
            <Row gutter={[8, 0]}>
              <Col xs={24}>
                <Form.Item<FieldType>
                  label="Email"
                  name="email"
                  rules={[
                    { required: true, message: 'Please input your email' },
                  ]}
                >
                  <Input />
                </Form.Item>
              </Col>
              <Col xs={24}>
                <Form.Item<FieldType>
                  label="Password"
                  name="password"
                  rules={[
                    { required: true, message: 'Please input your password!' },
                  ]}
                >
                  <Input.Password />
                </Form.Item>
              </Col>
              <Col xs={24}>
                <Form.Item<FieldType> name="remember" valuePropName="checked">
                  <Checkbox>Remember me</Checkbox>
                </Form.Item>
              </Col>
            </Row>
            <Form.Item>
              <Flex align="center" justify="space-between">
                <Button
                  type="primary"
                  htmlType="submit"
                  size="middle"
                  loading={loading}
                >
                  Login
                </Button>
                <Link to={PATH_AUTH.passwordReset}>Forgot password?</Link>
              </Flex>
            </Form.Item>
          </Form>
        </Flex>
      </Col>
    </Row>
  );
};
