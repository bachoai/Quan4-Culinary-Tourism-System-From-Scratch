import { useQuery } from '@tanstack/react-query';
import { Card, Select, Space, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import { analyticsApi } from '../api/analyticsApi';
import { poiApi } from '../api/poiApi';
import { EmptyState } from '../components/common/EmptyState';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';
import { formatDateTime } from '../utils/format';

const EVENT_OPTIONS = ['poi_viewed', 'audio_played', 'search_executed', 'nearby_requested', 'language_changed'];

export function UsageHistoryPage() {
  const { t } = useI18n();
  const [eventName, setEventName] = useState<string | undefined>();
  const [poiId, setPoiId] = useState<string | undefined>();
  const [lang, setLang] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const poisQuery = useQuery({ queryKey: ['pois'], queryFn: () => poiApi.loadAll() });
  const historyQuery = useQuery({
    queryKey: ['usage-history', page, pageSize, eventName, poiId, lang],
    queryFn: () => analyticsApi.history({ page, pageSize, eventName, poiId, lang }),
  });

  if (historyQuery.isLoading && !historyQuery.data) {
    return <LoadingScreen />;
  }

  return (
    <PageContainer title={t('usage_history_title')} subtitle={t('usage_history_subtitle')}>
      <Card className="glass-card">
        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            allowClear
            placeholder={t('status')}
            style={{ width: 180 }}
            options={EVENT_OPTIONS.map((value) => ({ value, label: value }))}
            onChange={(value) => { setEventName(value); setPage(1); }}
          />
          <Select
            allowClear
            placeholder={t('choose_poi')}
            style={{ width: 260 }}
            options={(poisQuery.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
            onChange={(value) => { setPoiId(value); setPage(1); }}
          />
          <Select
            allowClear
            placeholder={t('language')}
            style={{ width: 120 }}
            options={['vi', 'en', 'zh', 'ja', 'ko'].map((value) => ({ value, label: value.toUpperCase() }))}
            onChange={(value) => { setLang(value); setPage(1); }}
          />
        </Space>
        {(historyQuery.data?.items.length ?? 0) > 0 ? (
          <Table
            rowKey="id"
            dataSource={historyQuery.data?.items ?? []}
            loading={historyQuery.isFetching}
            pagination={{
              current: historyQuery.data?.page,
              pageSize: historyQuery.data?.pageSize,
              total: historyQuery.data?.totalItems,
              onChange: (nextPage, nextPageSize) => {
                setPage(nextPage);
                setPageSize(nextPageSize);
              },
            }}
            columns={[
              { title: 'Event', dataIndex: 'eventName', render: (value) => <Tag color="blue">{value}</Tag> },
              { title: 'POI', dataIndex: 'poiId', render: (value) => value ?? '--' },
              { title: t('language'), dataIndex: 'lang', render: (value) => value ?? '--' },
              { title: 'Session', dataIndex: 'sessionId', render: (value) => value ?? '--' },
              { title: t('created_at'), render: (_, record) => formatDateTime(record.createdAt) },
              {
                title: 'Metadata',
                render: (_, record) => (
                  <Typography.Paragraph copyable={{ text: JSON.stringify(record.metadata, null, 2) }} style={{ marginBottom: 0 }}>
                    <pre style={{ margin: 0, whiteSpace: 'pre-wrap' }}>{JSON.stringify(record.metadata, null, 2)}</pre>
                  </Typography.Paragraph>
                ),
              },
            ]}
          />
        ) : (
          <EmptyState title={t('usage_history_empty')} description={t('usage_history_empty_desc')} />
        )}
      </Card>
    </PageContainer>
  );
}
