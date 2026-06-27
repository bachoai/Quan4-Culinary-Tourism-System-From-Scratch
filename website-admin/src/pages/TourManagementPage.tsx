import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, Modal, Space, Table, Tag, Typography } from 'antd';
import { Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { tourApi } from '../api/tourApi';
import { ConfirmDeleteButton } from '../components/common/ConfirmDeleteButton';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { TourForm } from '../components/forms/TourForm';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';
import type { TourResponse } from '../types/responses';
import { formatDateTime } from '../utils/format';

export function TourManagementPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [editing, setEditing] = useState<TourResponse | null>(null);
  const [open, setOpen] = useState(false);

  const toursQuery = useQuery({ queryKey: ['tours'], queryFn: tourApi.getAll });

  const createMutation = useMutation({
    mutationFn: tourApi.create,
    onSuccess: () => {
      notification.success({ message: t('tour_created') });
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['tours'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Parameters<typeof tourApi.update>[1] }) => tourApi.update(id, payload),
    onSuccess: () => {
      notification.success({ message: t('tour_updated') });
      setOpen(false);
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ['tours'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: tourApi.delete,
    onSuccess: () => {
      notification.success({ message: t('tour_deleted') });
      queryClient.invalidateQueries({ queryKey: ['tours'] });
    },
  });

  const data = useMemo(() => {
    const items = toursQuery.data ?? [];
    if (!keyword.trim()) {
      return items;
    }

    const normalizedKeyword = keyword.toLowerCase();
    return items.filter((item) => `${item.title} ${item.description} ${item.lang}`.toLowerCase().includes(normalizedKeyword));
  }, [keyword, toursQuery.data]);

  if (toursQuery.isLoading) {
    return <LoadingScreen />;
  }

  return (
    <PageContainer
      title={t('tours_title')}
      subtitle={t('tours_subtitle')}
      extra={(
        <Space>
          <Input.Search placeholder={t('tours_search')} allowClear onChange={(event) => setKeyword(event.target.value)} />
          <Button type="primary" icon={<Plus size={16} />} onClick={() => { setEditing(null); setOpen(true); }}>
            {t('tours_new')}
          </Button>
        </Space>
      )}
    >
      <Card className="glass-card">
        <Table
          rowKey="id"
          dataSource={data}
          loading={toursQuery.isFetching}
          columns={[
            { title: t('name'), dataIndex: 'title' },
            { title: t('language'), dataIndex: 'lang' },
            { title: t('tour_duration_minutes'), dataIndex: 'estimatedDurationMinutes' },
            { title: t('tour_stop_count'), render: (_, record) => record.stops.length },
            { title: t('updated_at'), render: (_, record) => formatDateTime(record.updatedAt) },
            { title: t('status'), render: (_, record) => <Tag color={record.isActive ? 'green' : 'red'}>{record.isActive ? t('active') : t('inactive')}</Tag> },
            {
              title: t('actions'),
              render: (_, record) => (
                <Space>
                  <Button onClick={() => { setEditing(record); setOpen(true); }}>{t('edit')}</Button>
                  <ConfirmDeleteButton onConfirm={() => deleteMutation.mutate(record.id)} loading={deleteMutation.isPending} />
                </Space>
              ),
            },
          ]}
          expandable={{
            expandedRowRender: (record) => (
              <Space direction="vertical" style={{ width: '100%' }}>
                <Typography.Text>{record.description}</Typography.Text>
                <Typography.Text type="secondary">{t('tour_stops')}</Typography.Text>
                {record.stops
                  .slice()
                  .sort((left, right) => left.order - right.order)
                  .map((stop) => (
                    <Typography.Text key={`${record.id}-${stop.poiId}-${stop.order}`}>
                      {stop.order + 1}. {stop.title || stop.poiId} ({stop.poiId}) - {stop.estimatedStayMinutes}m
                    </Typography.Text>
                  ))}
              </Space>
            ),
          }}
        />
      </Card>
      <Modal open={open} onCancel={() => { setOpen(false); setEditing(null); }} footer={null} title={editing ? t('tour_edit') : t('tour_create')} destroyOnClose>
        <TourForm
          initialValues={editing}
          loading={createMutation.isPending || updateMutation.isPending}
          onSubmit={async (values) => {
            if (editing) {
              await updateMutation.mutateAsync({ id: editing.id, payload: values });
              return;
            }

            await createMutation.mutateAsync(values);
          }}
        />
      </Modal>
    </PageContainer>
  );
}
