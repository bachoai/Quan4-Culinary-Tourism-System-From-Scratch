import { useQuery } from '@tanstack/react-query';
import { Button, Card, Col, Row, Space, Typography } from 'antd';
import { Activity, ArrowRight, ChartColumnBig, ClipboardList, Landmark, PlayCircle, PlusCircle, Radio, ScrollText, UserRound, UsersRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { adminApi } from '../api/adminApi';
import { PageContainer } from '../components/layout/PageContainer';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { StatCard } from '../components/common/StatCard';
import { useI18n } from '../i18n/provider';
import { formatNumber } from '../utils/format';

export function DashboardPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const statsQuery = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: adminApi.getDashboardStats,
    refetchInterval: 10000,
    refetchIntervalInBackground: true,
  });

  if (statsQuery.isLoading) {
    return <LoadingScreen />;
  }

  const stats = statsQuery.data;
  if (!stats) return null;

  const chartData = [
    { name: 'Users', value: stats.totalUsers },
    { name: 'Owners', value: stats.totalOwners },
    { name: 'POIs', value: stats.totalPois },
    { name: 'Active POI', value: stats.totalActivePois },
    { name: 'Pending owner', value: stats.pendingOwnerRegistrations },
    { name: 'Pending submission', value: stats.pendingSubmissions },
  ];

  const quickActions = [
    { title: 'Tạo POI mới', description: 'Thêm địa điểm mới cho bản demo', icon: <PlusCircle size={18} />, to: '/admin/pois/create' },
    { title: 'Duyệt owner', description: 'Kiểm tra yêu cầu đăng ký đối tác', icon: <UsersRound size={18} />, to: '/admin/owner-registrations' },
    { title: 'Duyệt submissions', description: 'Xử lý đề xuất cập nhật nội dung', icon: <ClipboardList size={18} />, to: '/admin/submissions' },
    { title: 'Xem analytics', description: 'Theo dõi mức độ tương tác demo', icon: <ChartColumnBig size={18} />, to: '/admin/analytics' },
  ];

  return (
    <PageContainer title={t('dashboard_title')} subtitle={t('dashboard_subtitle')}>
      <div className="dashboard-grid">
        <StatCard title={t('dashboard_total_poi')} value={formatNumber(stats.totalPois)} prefix={<Landmark size={18} />} accent="#FF6B35" subtitle="Điểm đến ẩm thực trong hệ thống" />
        <StatCard title={t('dashboard_active_poi')} value={formatNumber(stats.totalActivePois)} prefix={<Activity size={18} />} accent="#2EC4B6" subtitle="Đang hiển thị cho người dùng cuối" />
        <StatCard title={t('dashboard_users')} value={formatNumber(stats.totalUsers)} prefix={<UsersRound size={18} />} accent="#4F46E5" subtitle="Tổng tài khoản đã đăng ký" />
        <StatCard title={t('dashboard_owners')} value={formatNumber(stats.totalOwners)} prefix={<UserRound size={18} />} accent="#F59E0B" subtitle="Đối tác sở hữu nội dung" />
        <StatCard title={t('dashboard_pending_owners')} value={formatNumber(stats.pendingOwnerRegistrations)} prefix={<UsersRound size={18} />} accent="#FB7185" subtitle="Cần kiểm tra và phản hồi" />
        <StatCard title={t('dashboard_pending_submissions')} value={formatNumber(stats.pendingSubmissions)} prefix={<ScrollText size={18} />} accent="#8B5CF6" subtitle="Đề xuất cập nhật đang chờ duyệt" />
        <StatCard title={t('dashboard_poi_views')} value={formatNumber(stats.totalPoiViews)} prefix={<Activity size={18} />} accent="#0EA5E9" subtitle="Lượt xem chi tiết điểm đến" />
        <StatCard title={t('dashboard_audio_plays')} value={formatNumber(stats.totalAudioPlays)} prefix={<PlayCircle size={18} />} accent="#14B8A6" subtitle="Lượt phát audio thuyết minh" />
        <StatCard
          title={t('dashboard_active_visitors_now')}
          value={formatNumber(stats.activeVisitorsNow)}
          prefix={<Radio size={18} />}
          accent="#E11D48"
          subtitle={`${formatNumber(stats.anonymousVisitorsNow)} ${t('dashboard_anonymous_visitors_now').toLowerCase()} · ${t('dashboard_active_window_label')}: ${stats.activeWindowSeconds}s`}
        />
      </div>
      <Row gutter={18}>
        <Col xs={24} lg={16}>
          <Card className="glass-card chart-card" title={t('dashboard_platform_activity')}>
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--chart-grid)" />
                <XAxis dataKey="name" stroke="var(--muted-text)" />
                <YAxis stroke="var(--muted-text)" />
                <Tooltip contentStyle={{ borderRadius: 16, border: '1px solid var(--glass-border)', background: 'var(--app-surface)', color: 'var(--app-text)' }} />
                <Bar dataKey="value" fill="#FF6B35" radius={[8, 8, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Card className="glass-card">
              <Typography.Title level={4}>{t('dashboard_quick_actions')}</Typography.Title>
              <Typography.Paragraph type="secondary">
                {t('dashboard_quick_actions_desc')}
              </Typography.Paragraph>
              <div className="dashboard-quick-actions">
                {quickActions.map((item) => (
                  <Button
                    key={item.to}
                    className="quick-action-button"
                    icon={item.icon}
                    onClick={() => navigate(item.to)}
                  >
                    <Space direction="vertical" size={0} style={{ alignItems: 'flex-start' }}>
                      <Typography.Text strong>{item.title}</Typography.Text>
                      <Typography.Text type="secondary">{item.description}</Typography.Text>
                    </Space>
                    <ArrowRight size={16} style={{ marginLeft: 'auto' }} />
                  </Button>
                ))}
              </div>
            </Card>
            <Card className="glass-card">
              <Typography.Title level={4}>{t('dashboard_status_snapshot')}</Typography.Title>
              <Typography.Paragraph style={{ marginBottom: 8 }}>{t('dashboard_waiting_owner')}: {stats.pendingOwnerRegistrations}</Typography.Paragraph>
              <Typography.Paragraph style={{ marginBottom: 0 }}>{t('dashboard_waiting_submission')}: {stats.pendingSubmissions}</Typography.Paragraph>
            </Card>
          </Space>
        </Col>
      </Row>
    </PageContainer>
  );
}
