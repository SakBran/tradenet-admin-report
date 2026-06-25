import React, {
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { ConfigProvider, Layout, Menu, MenuProps, SiderProps } from 'antd';
import { HistoryOutlined } from '@ant-design/icons';
import { Logo } from '../../components';
import { Link, useLocation } from 'react-router-dom';
import { PATH_DASHBOARD } from '../../constants';
import { COLOR } from '../../App.tsx';
import { useMediaQuery } from 'react-responsive';
import {
  getReportCategoryKey,
  reportCategoryKeys,
  reportNavItems,
} from '../../Report/reportNavItems.tsx';
import AuthContext from '../../context/AuthContext.tsx';
import './SideNav.css';

const { Sider } = Layout;

const rootSubmenuKeys = reportCategoryKeys;

type SideNavProps = SiderProps & {
  setCollapse: (value: React.SetStateAction<boolean>) => void;
};

const SideNav = ({ setCollapse, className, ...others }: SideNavProps) => {
  const nodeRef = useRef(null);
  const { pathname } = useLocation();
  const [openKeys, setOpenKeys] = useState(['']);
  const [current, setCurrent] = useState('');
  const isMobile = useMediaQuery({ maxWidth: 769 });
  const auth = useContext(AuthContext);

  // The activity-log view is admin-only; show its menu entry just for admins.
  const items = useMemo<MenuProps['items']>(() => {
    if (auth?.user?.permission === 'Admin') {
      return [
        {
          key: 'activity-log',
          icon: <HistoryOutlined />,
          label: <Link to="/ActivityLog/List">Activity Log</Link>,
        },
        ...reportNavItems,
      ];
    }
    return reportNavItems;
  }, [auth?.user?.permission]);

  const onClick: MenuProps['onClick'] = (e) => {
    console.log('click ', e);
    if (isMobile) {
      setCollapse(true);
    }
  };

  const onOpenChange: MenuProps['onOpenChange'] = (keys) => {
    const latestOpenKey = keys.find((key) => openKeys.indexOf(key) === -1);
    if (latestOpenKey && rootSubmenuKeys.indexOf(latestOpenKey!) === -1) {
      setOpenKeys(keys);
    } else {
      setOpenKeys(latestOpenKey ? [latestOpenKey] : []);
    }
  };

  useEffect(() => {
    const paths = pathname.split('/');
    if (paths[1] === 'Report') {
      const reportCategoryKey = getReportCategoryKey(paths[2]);
      setOpenKeys(reportCategoryKey ? [reportCategoryKey] : []);
    }
    setCurrent(paths[paths.length - 1]);
  }, [pathname]);

  return (
    <Sider
      ref={nodeRef}
      breakpoint="lg"
      collapsedWidth="0"
      className={`app-side-nav ${className ?? ''}`.trim()}
      {...others}
    >
      <Logo
        color="blue"
        asLink
        href={PATH_DASHBOARD.default}
        justify="center"
        imgSize={{ h: 40 }}
        style={{
          height: 64,
          padding: '0 1rem',
          borderBottom: '1px solid #d9d9d9',
        }}
      />
      <ConfigProvider
        theme={{
          components: {
            Menu: {
              itemBg: 'none',
              itemSelectedBg: COLOR['100'],
              itemHoverBg: COLOR['50'],
              itemSelectedColor: COLOR['600'],
            },
          },
        }}
      >
        <Menu
          className="app-side-nav-menu"
          mode="inline"
          items={items}
          onClick={onClick}
          openKeys={openKeys}
          onOpenChange={onOpenChange}
          selectedKeys={[current]}
          style={{ border: 'none' }}
        />
      </ConfigProvider>
    </Sider>
  );
};

export default SideNav;
