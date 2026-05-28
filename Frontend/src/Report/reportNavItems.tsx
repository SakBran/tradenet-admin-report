import { MenuProps } from 'antd';
import { Link } from 'react-router-dom';
import { reportConfigList } from './config/reportConfigs';

export const reportNavItems: Required<MenuProps>['items'] = reportConfigList.map(
  (config) => ({
    key: config.controllerName,
    label: <Link to={`/Report/${config.controllerName}`}>{config.title}</Link>,
  })
);
