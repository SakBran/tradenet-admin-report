import { ReactNode, useContext } from 'react';
import { Navigate } from 'react-router-dom';
import AuthContext from '../context/AuthContext';
import { canAccessReportRoute } from '../Report/reportNavItems';

const AccessGate = ({
  reportKey,
  adminOnly = false,
  children,
}: {
  reportKey?: string;
  adminOnly?: boolean;
  children: ReactNode;
}) => {
  const auth = useContext(AuthContext);
  if (!auth?.isAuthenticated) return <Navigate to="/auth/signin" replace />;
  if (!auth.access) return <div>Loading permissions...</div>;
  if (adminOnly && !auth.access.isAdmin) return <Navigate to="/errors/403" replace />;
  if (reportKey && !canAccessReportRoute(auth.access, reportKey))
    return <Navigate to="/errors/403" replace />;
  return <>{children}</>;
};

export default AccessGate;
