import React, { createContext, useEffect, useState, ReactNode } from 'react';
import axiosInstance from '../services/AxiosInstance';
import type { ReportAccess } from '../Report/reportNavItems';

interface User {
  id: string;
  permission: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  access: ReportAccess | null;
  login: (name: string, password: string) => Promise<boolean>;
  logout: () => void;
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [user, setUser] = useState<User | null>(() => {
    const id = localStorage.getItem('userid');
    const permission = localStorage.getItem('permission');

    if (id && permission) {
      return { id, permission };
    }
    return null;
  });

  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem('token')
  );
  const [access, setAccess] = useState<ReportAccess | null>(null);

  useEffect(() => {
    if (!token) return;
    let current = true;
    setAccess(null);
    axiosInstance.get<ReportAccess>('Auth/permissions')
      .then((response) => {
        if (current) setAccess(response.data);
      })
      .catch(() => {
        if (!current) return;
        localStorage.removeItem('token');
        localStorage.removeItem('userid');
        localStorage.removeItem('permission');
        setToken(null);
        setUser(null);
        setAccess(null);
      });
    return () => { current = false; };
  }, [token]);

  const login = async (name: string, password: string): Promise<boolean> => {
    // Replace with real API call
    const PostBody = {
      Name: name,
      Password: password,
      Permission: 'User',
    };
    try {
      const resp = await axiosInstance.post('Auth', PostBody);
      const temp = await resp.data;
      if (!temp || !temp.token) {
        return false;
      }

      localStorage.setItem('token', temp.token);
      localStorage.setItem('userid', temp.userId);
      localStorage.setItem('permission', temp.permission);

      setToken(temp.token);
      const user: User = {
        id: temp.userId,
        permission: temp.permission,
      };
      setUser(user);
      return true;
    } catch (ex) {
      console.log(ex);
      return false;
    }
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userid');
    localStorage.removeItem('permission');
    setToken(null);
    setUser(null);
    setAccess(null);
  };

  return (
    <AuthContext.Provider
      value={{ user, token, access, login, logout, isAuthenticated: !!user && !!token }}
    >
      {children}
    </AuthContext.Provider>
  );
};

// Optional for cleaner imports
export default AuthContext;
