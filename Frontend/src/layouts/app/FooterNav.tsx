import { Layout } from 'antd';

const { Footer } = Layout;

type FooterNavProps = React.HTMLAttributes<HTMLDivElement>;

const FooterNav = ({ ...others }: FooterNavProps) => {
  return (
    <Footer {...others}>
      T2.0 Report © 2026 Ministry of Commerce. All rights reserved.
    </Footer>
  );
};

export default FooterNav;
