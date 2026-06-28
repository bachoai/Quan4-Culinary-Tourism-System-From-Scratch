import { Drawer, Layout, Menu } from 'antd';
import {
  AudioLines,
  ChartColumn,
  FolderKanban,
  History,
  Landmark,
  LayoutDashboard,
  Map,
  Mic2,
  QrCode,
  Route,
  ScrollText,
  Tags,
  Users,
} from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useI18n } from '../../i18n/provider';

interface SidebarProps {
  collapsed: boolean;
  mobile?: boolean;
  open?: boolean;
  onClose?: () => void;
}

export function Sidebar({ collapsed, mobile = false, open = false, onClose }: SidebarProps) {
  const { t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const items = [
    { key: '/admin/dashboard', icon: <LayoutDashboard size={16} />, label: t('sidebar_dashboard') },
    { key: '/admin/categories', icon: <Tags size={16} />, label: t('sidebar_categories') },
    { key: '/admin/pois', icon: <Landmark size={16} />, label: t('sidebar_pois') },
    { key: '/admin/owner-registrations', icon: <FolderKanban size={16} />, label: t('sidebar_owner_registrations') },
    { key: '/admin/submissions', icon: <ScrollText size={16} />, label: t('sidebar_submissions') },
    { key: '/admin/users', icon: <Users size={16} />, label: t('sidebar_users') },
    { key: '/admin/audio', icon: <Mic2 size={16} />, label: t('sidebar_audio') },
    { key: '/admin/localizations', icon: <AudioLines size={16} />, label: t('sidebar_localizations') },
    { key: '/admin/analytics', icon: <ChartColumn size={16} />, label: t('sidebar_analytics') },
    { key: '/admin/qr-activations', icon: <QrCode size={16} />, label: t('sidebar_qr_activations') },
    { key: '/admin/usage-history', icon: <History size={16} />, label: t('sidebar_usage_history') },
    { key: '/admin/tours', icon: <Route size={16} />, label: t('sidebar_tours') },
    { key: '/admin/maps', icon: <Map size={16} />, label: t('sidebar_maps') },
  ];
  const selectedKey = items.find((item) => location.pathname.startsWith(item.key))?.key ?? '/admin/dashboard';
  const sidebarContent = (
    <>
      <div className="brand-block">
        <div className="brand-badge">Q4</div>
        {!collapsed ? (
          <div>
            <div className="brand-title">Quan4 Admin</div>
            <div className="brand-subtitle">Culinary Tourism</div>
          </div>
        ) : null}
      </div>
      <Menu
        mode="inline"
        selectedKeys={[selectedKey]}
        items={items}
        onClick={({ key }) => {
          navigate(key);
          onClose?.();
        }}
        className="sidebar-menu"
      />
    </>
  );

  if (mobile) {
    return (
      <Drawer
        placement="left"
        width={288}
        open={open}
        onClose={onClose}
        closable={false}
        className="sidebar-drawer"
        rootClassName="sidebar-drawer-root"
        styles={{ body: { padding: 0 } }}
      >
        <div className="app-sider app-sider-mobile">{sidebarContent}</div>
      </Drawer>
    );
  }

  return (
    <Layout.Sider width={272} collapsedWidth={96} collapsible trigger={null} collapsed={collapsed} className="app-sider">
      {sidebarContent}
    </Layout.Sider>
  );
}
