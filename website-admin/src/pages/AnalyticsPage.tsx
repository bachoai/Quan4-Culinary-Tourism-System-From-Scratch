import { useQuery } from '@tanstack/react-query';
import { Card, Col, Row, Space, Tag, Typography } from 'antd';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from 'recharts';
import { analyticsApi } from '../api/analyticsApi';
import { poiApi } from '../api/poiApi';
import { EmptyState } from '../components/common/EmptyState';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { StatCard } from '../components/common/StatCard';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';
import { formatDateTime, formatNumber } from '../utils/format';
import { getMediaUrlOrPlaceholder } from '../utils/media';

type PoiLookupItem = {
  id: string;
  name: string;
  address: string;
  district: string;
  images: Array<{ url: string; isThumbnail?: boolean }>;
};

type PoiChartDatum = {
  poiId: string;
  name: string;
  shortName: string;
  address: string;
  imageUrl: string;
  metricLabel: string;
  value: number;
};

type EventChartDatum = {
  name: string;
  value: number;
  color: string;
  description: string;
};

function shortenLabel(label: string, maxLength = 18) {
  return label.length <= maxLength ? label : `${label.slice(0, maxLength - 1)}…`;
}

function getPoiThumbnailUrl(poi?: PoiLookupItem) {
  return getMediaUrlOrPlaceholder(poi?.images.find((image) => image.isThumbnail)?.url || poi?.images[0]?.url);
}

function PoiAnalyticsTooltip({
  active,
  payload,
}: {
  active?: boolean;
  payload?: Array<{ value?: number | string; payload?: PoiChartDatum }>;
}) {
  const datum = payload?.[0]?.payload;
  if (!active || !datum) {
    return null;
  }

  return (
    <div
      style={{
        minWidth: 280,
        maxWidth: 340,
        overflow: 'hidden',
        borderRadius: 18,
        border: '1px solid var(--glass-border)',
        background: 'var(--app-surface)',
        boxShadow: '0 18px 38px rgba(15, 23, 42, 0.18)',
      }}
    >
      <img
        src={datum.imageUrl}
        alt={datum.name}
        style={{ display: 'block', width: '100%', height: 140, objectFit: 'cover' }}
      />
      <div style={{ padding: 14 }}>
        <div style={{ color: 'var(--app-text)', fontSize: 15, fontWeight: 700 }}>{datum.name}</div>
        <div style={{ marginTop: 4, color: 'var(--muted-text)', fontSize: 12, lineHeight: 1.5 }}>{datum.address}</div>
        <div style={{ marginTop: 10, color: 'var(--app-text)', fontSize: 13 }}>
          <strong>{datum.metricLabel}:</strong> {formatNumber(datum.value)}
        </div>
      </div>
    </div>
  );
}

function EventDistributionTooltip({
  active,
  payload,
}: {
  active?: boolean;
  payload?: Array<{ value?: number | string; payload?: EventChartDatum }>;
}) {
  const datum = payload?.[0]?.payload;
  if (!active || !datum) {
    return null;
  }

  return (
    <div
      style={{
        minWidth: 240,
        borderRadius: 18,
        border: '1px solid var(--glass-border)',
        background: 'var(--app-surface)',
        boxShadow: '0 18px 38px rgba(15, 23, 42, 0.18)',
        padding: 14,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <span
          style={{
            display: 'inline-block',
            width: 12,
            height: 12,
            borderRadius: 999,
            background: datum.color,
          }}
        />
        <div style={{ color: 'var(--app-text)', fontSize: 15, fontWeight: 700 }}>{datum.name}</div>
      </div>
      <div style={{ marginTop: 6, color: 'var(--muted-text)', fontSize: 12, lineHeight: 1.5 }}>{datum.description}</div>
      <div style={{ marginTop: 10, color: 'var(--app-text)', fontSize: 13 }}>
        <strong>So luot:</strong> {formatNumber(datum.value)}
      </div>
    </div>
  );
}

export function AnalyticsPage() {
  const { t } = useI18n();
  const summaryQuery = useQuery({ queryKey: ['analytics-summary'], queryFn: analyticsApi.summary });
  const poiQuery = useQuery({ queryKey: ['analytics-poi-lookup'], queryFn: () => poiApi.loadAll({ lang: 'vi' }) });

  if (summaryQuery.isLoading || !summaryQuery.data) {
    return <LoadingScreen />;
  }

  const poiLookup = Object.fromEntries(
    (poiQuery.data || []).map((poi) => [
      poi.id,
      {
        id: poi.id,
        name: poi.name,
        address: poi.address,
        district: poi.district,
        images: poi.images,
      } satisfies PoiLookupItem,
    ]),
  ) as Record<string, PoiLookupItem>;

  const topViewData: PoiChartDatum[] = summaryQuery.data.topPoiViews.map((item, index) => {
    const poi = poiLookup[item.poiId];
    const name = poi?.name || `POI ${index + 1}`;
    return {
      poiId: item.poiId,
      name,
      shortName: shortenLabel(name),
      address: poi ? `${poi.address}, ${poi.district}` : item.poiId,
      imageUrl: getPoiThumbnailUrl(poi),
      metricLabel: 'Luot xem',
      value: item.count,
    };
  });

  const topAudioData: PoiChartDatum[] = summaryQuery.data.topPoiAudioPlays.map((item, index) => {
    const poi = poiLookup[item.poiId];
    const name = poi?.name || `POI ${index + 1}`;
    return {
      poiId: item.poiId,
      name,
      shortName: shortenLabel(name),
      address: poi ? `${poi.address}, ${poi.district}` : item.poiId,
      imageUrl: getPoiThumbnailUrl(poi),
      metricLabel: 'Luot nghe',
      value: item.count,
    };
  });

  const eventDistributionData: EventChartDatum[] = [
    {
      name: 'Mo chi tiet POI',
      value: summaryQuery.data.poiViewedCount,
      color: '#FF6B35',
      description: 'So lan nguoi dung mo vao trang chi tiet cua dia diem.',
    },
    {
      name: 'Nghe audio',
      value: summaryQuery.data.audioPlayedCount,
      color: '#2EC4B6',
      description: 'So lan phat audio file hoac thuyet minh tren trinh duyet.',
    },
    {
      name: 'Tim kiem',
      value: summaryQuery.data.searchExecutedCount,
      color: '#4F46E5',
      description: 'So lan nguoi dung chu dong tim kiem noi dung trong app.',
    },
  ];

  const heatmapData = summaryQuery.data.heatmapPoints.map((item) => ({
    lng: item.longitude,
    lat: item.latitude,
    count: item.count,
  }));

  const hasEventDistribution = eventDistributionData.some((item) => item.value > 0);

  return (
    <PageContainer title={t('analytics_title')} subtitle={t('analytics_subtitle')}>
      <div className="analytics-metrics">
        <StatCard
          title={t('dashboard_poi_views')}
          value={formatNumber(summaryQuery.data.poiViewedCount)}
          accent="#FF6B35"
          subtitle="So lan nguoi dung mo vao mot dia diem cu the."
        />
        <StatCard
          title={t('dashboard_audio_plays')}
          value={formatNumber(summaryQuery.data.audioPlayedCount)}
          accent="#2EC4B6"
          subtitle="So lan phat audio va thuyet minh tren giao dien."
        />
        <StatCard
          title="Luot tim kiem"
          value={formatNumber(summaryQuery.data.searchExecutedCount)}
          accent="#4F46E5"
          subtitle="So lan tim kiem mon an, quan an hoac khu vuc."
        />
        <StatCard
          title={t('analytics_average_listen_duration')}
          value={`${summaryQuery.data.averageListenDurationSeconds.toFixed(1)}s`}
          accent="#E76F51"
          subtitle="Thoi gian nghe trung binh tren moi lan phat thuyet minh."
        />
        <Card className="glass-card">
          <Typography.Title level={5}>Goc nhin nhanh</Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            Khi re chuot vao cot va bieu do tron, admin se thay ngay ten dia diem, anh dai dien va y nghia cua tung nhom du lieu.
          </Typography.Paragraph>
        </Card>
      </div>

      <Row gutter={18}>
        <Col xs={24} lg={14}>
          <Card className="glass-card chart-card" title={t('analytics_top_poi_views')}>
            {topViewData.length ? (
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={topViewData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--chart-grid)" />
                  <XAxis dataKey="shortName" stroke="var(--muted-text)" interval={0} angle={-12} textAnchor="end" height={64} />
                  <YAxis stroke="var(--muted-text)" allowDecimals={false} />
                  <Tooltip content={<PoiAnalyticsTooltip />} />
                  <Bar dataKey="value" name="Luot xem" fill="#FF6B35" radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="chart-empty">
                <EmptyState
                  title="Chua co du lieu luot xem"
                  description="Khi nguoi dung mo chi tiet dia diem, bieu do nay se hien ten dia diem de nhin nhanh hon."
                />
              </div>
            )}
          </Card>
        </Col>

        <Col xs={24} lg={10}>
          <Card className="glass-card chart-card" title={t('analytics_event_distribution')}>
            {hasEventDistribution ? (
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie
                    data={eventDistributionData}
                    dataKey="value"
                    nameKey="name"
                    outerRadius={96}
                    label={({ name }) => shortenLabel(String(name), 14)}
                  >
                    {eventDistributionData.map((item) => (
                      <Cell key={item.name} fill={item.color} />
                    ))}
                  </Pie>
                  <Tooltip content={<EventDistributionTooltip />} />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <div className="chart-empty">
                <EmptyState
                  title="Bieu do dang trong"
                  description="Hien chua co luong su kien du de ve phan bo event de nhin nhanh."
                />
              </div>
            )}
          </Card>
        </Col>
      </Row>

      <Row gutter={18} style={{ marginTop: 18 }}>
        <Col xs={24} lg={12}>
          <Card className="glass-card chart-card" title={t('analytics_top_poi_audio')}>
            {topAudioData.length ? (
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={topAudioData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--chart-grid)" />
                  <XAxis dataKey="shortName" stroke="var(--muted-text)" interval={0} angle={-12} textAnchor="end" height={64} />
                  <YAxis stroke="var(--muted-text)" allowDecimals={false} />
                  <Tooltip content={<PoiAnalyticsTooltip />} />
                  <Bar dataKey="value" name="Luot nghe" fill="#2EC4B6" radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="chart-empty">
                <EmptyState
                  title="Chua co du lieu luot nghe"
                  description="Khi nguoi dung phat audio hoac thuyet minh, bieu do nay se cho biet dia diem nao duoc nghe nhieu nhat."
                />
              </div>
            )}
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card className="glass-card chart-card" title={t('analytics_heatmap')}>
            {heatmapData.length ? (
              <ResponsiveContainer width="100%" height={300}>
                <ScatterChart margin={{ top: 12, right: 12, bottom: 12, left: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--chart-grid)" />
                  <XAxis type="number" dataKey="lng" name="Kinh do" stroke="var(--muted-text)" />
                  <YAxis type="number" dataKey="lat" name="Vi do" stroke="var(--muted-text)" />
                  <ZAxis type="number" dataKey="count" range={[60, 280]} />
                  <Tooltip
                    cursor={{ strokeDasharray: '3 3' }}
                    formatter={(value) => [formatNumber(Number(value ?? 0)), 'So mau tuong tac']}
                    labelFormatter={(_, payload) => {
                      const point = payload?.[0]?.payload as { lat: number; lng: number } | undefined;
                      return point ? `Vi do: ${point.lat.toFixed(5)} | Kinh do: ${point.lng.toFixed(5)}` : '';
                    }}
                    contentStyle={{
                      borderRadius: 16,
                      border: '1px solid var(--glass-border)',
                      background: 'var(--app-surface)',
                      color: 'var(--app-text)',
                    }}
                  />
                  <Scatter data={heatmapData} fill="#F4A261" />
                </ScatterChart>
              </ResponsiveContainer>
            ) : (
              <div className="chart-empty">
                <EmptyState title={t('analytics_heatmap')} description={t('analytics_heatmap_empty')} />
              </div>
            )}
          </Card>
        </Col>
      </Row>

      <Row gutter={18} style={{ marginTop: 18 }}>
        <Col span={24}>
          <Card className="glass-card" title={t('analytics_recent_routes')}>
            {summaryQuery.data.recentRouteTraces.length ? (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                {summaryQuery.data.recentRouteTraces.map((route) => (
                  <Card key={`${route.anonymousId}-${route.sessionId ?? 'no-session'}`} size="small">
                    <Space direction="vertical" style={{ width: '100%' }}>
                      <Space wrap>
                        <Tag color="blue">{route.anonymousId.slice(-8)}</Tag>
                        <Tag>{route.sessionId?.slice(-8) ?? 'no-session'}</Tag>
                        <Tag color="gold">{route.pointCount} points</Tag>
                      </Space>
                      <Typography.Text type="secondary">
                        {formatDateTime(route.startedAt)} {' -> '} {formatDateTime(route.endedAt)}
                      </Typography.Text>
                      <Typography.Paragraph style={{ marginBottom: 0 }}>
                        {route.points.map((point) => `(${point.latitude.toFixed(5)}, ${point.longitude.toFixed(5)})`).join(' -> ')}
                      </Typography.Paragraph>
                    </Space>
                  </Card>
                ))}
              </Space>
            ) : (
              <EmptyState title={t('analytics_recent_routes')} description={t('analytics_routes_empty')} />
            )}
          </Card>
        </Col>
      </Row>
    </PageContainer>
  );
}
