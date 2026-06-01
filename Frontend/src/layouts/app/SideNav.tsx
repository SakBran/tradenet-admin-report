import React, { useEffect, useRef, useState } from 'react';
import { ConfigProvider, Layout, Menu, MenuProps, SiderProps } from 'antd';
import { Logo } from '../../components';
import { useLocation } from 'react-router-dom';
import { PATH_DASHBOARD } from '../../constants';
import { COLOR } from '../../App.tsx';
import { useMediaQuery } from 'react-responsive';
import {
  getReportCategoryKey,
  reportCategoryKeys,
  reportNavItems,
} from '../../Report/reportNavItems.tsx';
import './SideNav.css';

const { Sider } = Layout;

const items: MenuProps['items'] = reportNavItems;
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
        gap="small"
        imgSize={{ h: 28, w: 28 }}
        style={{ padding: '1rem 0' }}
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
