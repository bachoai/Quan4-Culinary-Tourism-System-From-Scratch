import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Image, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { categoryApi } from '../api/categoryApi';
import { poiApi } from '../api/poiApi';
import { ConfirmDeleteButton } from '../components/common/ConfirmDeleteButton';
import { EmptyState } from '../components/common/EmptyState';
import { StatusBadge } from '../components/common/StatusBadge';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';
import { getMediaUrlOrPlaceholder } from '../utils/media';

export function PoiListPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [categoryId, setCategoryId] = useState<string | undefined>();
  const [priceRange, setPriceRange] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();

  const poiQuery = useQuery({ queryKey: ['pois'], queryFn: () => poiApi.loadAll() });
  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: categoryApi.getAll });

  const deleteMutation = useMutation({
    mutationFn: poiApi.delete,
    onSuccess: () => {
      notification.success({ message: t('pois_deleted') });
      queryClient.invalidateQueries({ queryKey: ['pois'] });
    },
  });

  const activeMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => poiApi.setActive(id, isActive),
    onSuccess: () => {
      notification.success({ message: t('pois_status_updated') });
      queryClient.invalidateQueries({ queryKey: ['pois'] });
    },
  });

  const filtered = useMemo(() => {
    return (poiQuery.data ?? []).filter((item) => {
      const matchesKeyword = !keyword || `${item.name} ${item.address}`.toLowerCase().includes(keyword.toLowerCase());
      const matchesCategory = !categoryId || item.categoryId === categoryId;
      const matchesPrice = !priceRange || item.priceRange === priceRange;
      const matchesStatus = !status || String(item.isActive) === status;
      return matchesKeyword && matchesCategory && matchesPrice && matchesStatus;
    });
  }, [categoryId, keyword, poiQuery.data, priceRange, status]);

  return (
    <PageContainer
      title={t('pois_title')}
      subtitle={t('pois_subtitle')}
      extra={
        <Button type="primary" icon={<Plus size={16} />} onClick={() => navigate('/admin/pois/create')}>
          {t('pois_add')}
        </Button>
      }
    >
      <Card className="glass-card">
        <Space wrap className="page-toolbar" style={{ marginBottom: 16 }}>
          <Input.Search placeholder={t('pois_search')} allowClear onChange={(event) => setKeyword(event.target.value)} />
          <Select
            allowClear
            placeholder={t('category')}
            style={{ width: 180 }}
            options={(categoriesQuery.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
            onChange={setCategoryId}
          />
          <Select allowClear placeholder={t('price')} style={{ width: 120 }} options={['$', '$$', '$$$'].map((value) => ({ value, label: value }))} onChange={setPriceRange} />
          <Select
            allowClear
            placeholder={t('status')}
            style={{ width: 120 }}
            options={[
              { value: 'true', label: t('active') },
              { value: 'false', label: t('inactive') },
            ]}
            onChange={setStatus}
          />
        </Space>
        <Table
          className="table-responsive"
          rowKey="id"
          dataSource={filtered}
          loading={poiQuery.isFetching}
          scroll={{ x: 1200 }}
          locale={{
            emptyText: (
              <EmptyState
                title="Chưa có POI phù hợp"
                description="Thử đổi bộ lọc hoặc tạo địa điểm mới cho demo khóa luận."
                action={
                  <Button type="primary" onClick={() => navigate('/admin/pois/create')}>
                    {t('pois_add')}
                  </Button>
                }
              />
            ),
          }}
          columns={[
            {
              title: t('thumbnail'),
              render: (_, record) => {
                const image = record.images.find((item) => item.isThumbnail) ?? record.images[0];
                return (
                  <Image
                    src={getMediaUrlOrPlaceholder(image?.url)}
                    fallback={getMediaUrlOrPlaceholder()}
                    width={56}
                    height={56}
                    className="thumbnail-image"
                    preview={Boolean(image?.url)}
                  />
                );
              },
            },
            {
              title: t('name'),
              dataIndex: 'name',
              render: (_, record) => (
                <Space direction="vertical" size={2}>
                  <Typography.Text strong>{record.name}</Typography.Text>
                  <Typography.Text type="secondary">{record.address}</Typography.Text>
                </Space>
              ),
            },
            { title: t('address'), dataIndex: 'address' },
            { title: t('ward'), dataIndex: 'ward' },
            {
              title: t('price'),
              dataIndex: 'priceRange',
              render: (value) => <span className="pill-badge">{value || 'N/A'}</span>,
            },
            {
              title: t('category'),
              dataIndex: 'categoryId',
              render: (value) => <Tag bordered={false} color="cyan">{(categoriesQuery.data ?? []).find((item) => item.id === value)?.name ?? value}</Tag>,
            },
            { title: t('rating'), dataIndex: 'rating' },
            {
              title: t('status'),
              render: (_, record) => <StatusBadge value={record.isActive} trueLabel={t('active')} falseLabel={t('inactive')} />,
            },
            {
              title: t('actions'),
              render: (_, record) => (
                <Space>
                  <Button onClick={() => navigate(`/admin/pois/${record.id}`)}>{t('pois_detail')}</Button>
                  <Button onClick={() => navigate(`/admin/pois/${record.id}/edit`)}>{t('edit')}</Button>
                  <Button onClick={() => activeMutation.mutate({ id: record.id, isActive: !record.isActive })}>
                    {record.isActive ? t('deactivate') : t('activate')}
                  </Button>
                  <ConfirmDeleteButton onConfirm={() => deleteMutation.mutate(record.id)} />
                </Space>
              ),
            },
          ]}
        />
      </Card>
    </PageContainer>
  );
}
