import { Grid, Layout } from 'antd';
import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function AdminLayout() {
  const screens = Grid.useBreakpoint();
  const isMobile = !screens.lg;
  const [collapsed, setCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);

  useEffect(() => {
    if (!isMobile) {
      setMobileSidebarOpen(false);
    }
  }, [isMobile]);

  return (
    <Layout className="app-layout">
      {isMobile ? (
        <Sidebar
          collapsed={false}
          mobile
          open={mobileSidebarOpen}
          onClose={() => setMobileSidebarOpen(false)}
        />
      ) : (
        <Sidebar collapsed={collapsed} />
      )}
      <Layout className="app-main-shell">
        <Header
          collapsed={collapsed}
          mobile={isMobile}
          onToggle={() => {
            if (isMobile) {
              setMobileSidebarOpen(true);
              return;
            }

            setCollapsed((value) => !value);
          }}
        />
        <Layout.Content className="app-content">
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
