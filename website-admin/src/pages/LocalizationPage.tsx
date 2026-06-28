import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Modal, Select, Space, Table } from 'antd';
import { useState } from 'react';
import { localizationApi } from '../api/localizationApi';
import { poiApi } from '../api/poiApi';
import { ConfirmDeleteButton } from '../components/common/ConfirmDeleteButton';
import { LocalizationForm } from '../components/forms/LocalizationForm';
import { EmptyState } from '../components/common/EmptyState';
import { useI18n } from '../i18n/provider';
import { PageContainer } from '../components/layout/PageContainer';

export function LocalizationPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [poiId, setPoiId] = useState<string>();
  const [open, setOpen] = useState(false);
  const poisQuery = useQuery({ queryKey: ['pois'], queryFn: () => poiApi.loadAll() });
  const localizationsQuery = useQuery({ queryKey: ['localizations', poiId], queryFn: () => localizationApi.getByPoi(poiId!), enabled: Boolean(poiId) });
  const createMutation = useMutation({
    mutationFn: ({ poiId, payload }: { poiId: string; payload: Parameters<typeof localizationApi.create>[1] }) => localizationApi.create(poiId, payload),
    onSuccess: () => {
      notification.success({ message: t('localization_created') });
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['localizations', poiId] });
    },
  });
  const deleteMutation = useMutation({
    mutationFn: ({ poiId, lang }: { poiId: string; lang: string }) => localizationApi.delete(poiId, lang),
    onSuccess: () => {
      notification.success({ message: t('localization_deleted') });
      queryClient.invalidateQueries({ queryKey: ['localizations', poiId] });
    },
  });

  return (
    <PageContainer title={t('localization_title')} subtitle={t('localization_subtitle')}>
      <Card className="glass-card">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap className="page-toolbar">
            <Select
              placeholder={t('choose_poi')}
              style={{ width: 320 }}
              options={(poisQuery.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
              onChange={setPoiId}
            />
            <Button type="primary" disabled={!poiId} onClick={() => setOpen(true)}>
              {t('add_localization')}
            </Button>
          </Space>
          {poiId ? (
            <Table
              className="table-responsive"
              rowKey="id"
              dataSource={localizationsQuery.data ?? []}
              loading={localizationsQuery.isFetching}
              scroll={{ x: 960 }}
              columns={[
                { title: t('language'), dataIndex: 'lang' },
                { title: t('name'), dataIndex: 'name' },
                { title: t('description'), dataIndex: 'description' },
                { title: t('tts_script'), dataIndex: 'ttsScript' },
                {
                  title: t('actions'),
                  render: (_, record) => (
                    <ConfirmDeleteButton onConfirm={() => deleteMutation.mutate({ poiId, lang: record.lang })} />
                  ),
                },
              ]}
            />
          ) : (
            <EmptyState title={t('no_localization_poi')} description={t('no_localization_poi_desc')} />
          )}
        </Space>
      </Card>
      <Modal open={open} onCancel={() => setOpen(false)} footer={null} title={t('add_localization')} destroyOnClose>
        <LocalizationForm loading={createMutation.isPending} onSubmit={async (payload) => poiId && createMutation.mutateAsync({ poiId, payload })} />
      </Modal>
    </PageContainer>
  );
}
