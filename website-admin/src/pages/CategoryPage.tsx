import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, Modal, Space, Table, Tag } from 'antd';
import { Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { categoryApi } from '../api/categoryApi';
import { ConfirmDeleteButton } from '../components/common/ConfirmDeleteButton';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { CategoryForm } from '../components/forms/CategoryForm';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';
import type { CategoryResponse } from '../types/responses';

export function CategoryPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [editing, setEditing] = useState<CategoryResponse | null>(null);
  const [open, setOpen] = useState(false);

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: categoryApi.getAll });

  const createMutation = useMutation({
    mutationFn: categoryApi.create,
    onSuccess: () => {
      notification.success({ message: t('categories_created') });
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['categories'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Parameters<typeof categoryApi.update>[1] }) =>
      categoryApi.update(id, payload),
    onSuccess: () => {
      notification.success({ message: t('categories_updated') });
      setOpen(false);
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ['categories'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: categoryApi.delete,
    onSuccess: () => {
      notification.success({ message: t('categories_deleted') });
      queryClient.invalidateQueries({ queryKey: ['categories'] });
    },
  });

  const data = useMemo(() => {
    const items = categoriesQuery.data ?? [];
    if (!keyword) return items;
    return items.filter((item) => `${item.code} ${item.name}`.toLowerCase().includes(keyword.toLowerCase()));
  }, [categoriesQuery.data, keyword]);

  if (categoriesQuery.isLoading) return <LoadingScreen />;

  return (
    <PageContainer
      title={t('categories_title')}
      subtitle={t('categories_subtitle')}
      extra={
        <Space wrap className="page-toolbar">
          <Input.Search placeholder={t('categories_search')} allowClear onChange={(event) => setKeyword(event.target.value)} />
          <Button
            type="primary"
            icon={<Plus size={16} />}
            onClick={() => {
              setEditing(null);
              setOpen(true);
            }}
          >
            {t('categories_new')}
          </Button>
        </Space>
      }
    >
      <Card className="glass-card">
        <Table
          className="table-responsive"
          rowKey="id"
          dataSource={data}
          loading={categoriesQuery.isFetching}
          scroll={{ x: 900 }}
          columns={[
            { title: t('code'), dataIndex: 'code' },
            { title: t('name'), dataIndex: 'name' },
            { title: t('description'), dataIndex: 'description' },
            { title: t('sort_order'), dataIndex: 'sortOrder' },
            {
              title: t('status'),
              render: (_, record) => <Tag color={record.isActive ? 'green' : 'red'}>{record.isActive ? t('active') : t('inactive')}</Tag>,
            },
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
        />
      </Card>
      <Modal open={open} onCancel={() => { setOpen(false); setEditing(null); }} footer={null} title={editing ? t('categories_edit') : t('categories_create')} destroyOnClose>
        <CategoryForm
          initialValues={
            editing
              ? {
                  code: editing.code,
                  name: editing.name,
                  description: editing.description ?? '',
                  iconUrl: editing.iconUrl ?? '',
                  sortOrder: editing.sortOrder,
                  isActive: editing.isActive,
                }
              : undefined
          }
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
