import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Descriptions, Input, Modal, Select, Space, Table } from 'antd';
import { useState } from 'react';
import { ownerApi } from '../api/ownerApi';
import { useI18n } from '../i18n/provider';
import { StatusBadge } from '../components/common/StatusBadge';
import { PageContainer } from '../components/layout/PageContainer';

export function OwnerRegistrationPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<string | undefined>();
  const [rejectId, setRejectId] = useState<string | null>(null);
  const [rejectNote, setRejectNote] = useState('');
  const [detailId, setDetailId] = useState<string | null>(null);

  const query = useQuery({ queryKey: ['owner-registrations', status], queryFn: () => ownerApi.getOwnerRegistrations(status) });
  const approveMutation = useMutation({
    mutationFn: (id: string) => ownerApi.approveOwner(id),
    onSuccess: () => {
      notification.success({ message: t('owner_approved') });
      queryClient.invalidateQueries({ queryKey: ['owner-registrations'] });
    },
  });
  const rejectMutation = useMutation({
    mutationFn: ({ id, adminNote }: { id: string; adminNote: string }) => ownerApi.rejectOwner(id, { adminNote }),
    onSuccess: () => {
      notification.success({ message: t('owner_rejected') });
      setRejectId(null);
      setRejectNote('');
      queryClient.invalidateQueries({ queryKey: ['owner-registrations'] });
    },
  });
  const disableMutation = useMutation({
    mutationFn: (id: string) => ownerApi.disableOwner(id),
    onSuccess: () => {
      notification.success({ message: t('owner_disabled') });
      queryClient.invalidateQueries({ queryKey: ['owner-registrations'] });
    },
  });

  const selectedRecord = (query.data ?? []).find((item) => item.id === detailId) ?? null;

  return (
    <PageContainer title={t('owner_registrations_title')} subtitle={t('owner_registrations_subtitle')}>
      <Card className="glass-card">
        <Space wrap className="page-toolbar" style={{ marginBottom: 16 }}>
          <Select
            allowClear
            placeholder={t('status')}
            style={{ width: 180 }}
            options={[
              { value: 'pending', label: t('pending') },
              { value: 'approved', label: t('approved') },
              { value: 'rejected', label: t('rejected') },
            ]}
            onChange={setStatus}
          />
        </Space>
        <Table
          className="table-responsive"
          rowKey="id"
          dataSource={query.data ?? []}
          loading={query.isFetching}
          scroll={{ x: 1080 }}
          columns={[
            { title: t('business'), dataIndex: 'businessName' },
            { title: t('business_address'), dataIndex: 'businessAddress' },
            { title: t('phone'), dataIndex: 'phoneNumber' },
            { title: t('description'), dataIndex: 'description' },
            { title: t('status'), render: (_, record) => <StatusBadge value={record.status} /> },
            {
              title: t('actions'),
              render: (_, record) => {
                if (record.status === 'rejected') {
                  return <Button onClick={() => setDetailId(record.id)}>{t('details')}</Button>;
                }

                if (record.status === 'approved') {
                  return (
                    <Space>
                      <Button onClick={() => setDetailId(record.id)}>{t('details')}</Button>
                      <Button
                        danger
                        onClick={() =>
                          Modal.confirm({
                            title: t('disable_owner_registration_confirm'),
                            content: record.businessName,
                            okButtonProps: { danger: true, loading: disableMutation.isPending },
                            onOk: () => disableMutation.mutate(record.id),
                          })
                        }
                      >
                        {t('disable_owner')}
                      </Button>
                    </Space>
                  );
                }

                return (
                  <Space>
                    <Button onClick={() => setDetailId(record.id)}>{t('details')}</Button>
                    <Button
                      type="primary"
                      onClick={() =>
                        Modal.confirm({
                          title: t('approve_owner_registration_confirm'),
                          content: record.businessName,
                          okButtonProps: { loading: approveMutation.isPending },
                          onOk: () => approveMutation.mutate(record.id),
                        })
                      }
                    >
                      {t('approve')}
                    </Button>
                    <Button danger onClick={() => setRejectId(record.id)}>{t('reject')}</Button>
                  </Space>
                );
              },
            },
          ]}
        />
      </Card>
      <Modal open={Boolean(rejectId)} onCancel={() => setRejectId(null)} onOk={() => rejectId && rejectNote.trim() && rejectMutation.mutate({ id: rejectId, adminNote: rejectNote.trim() })} okButtonProps={{ danger: true, loading: rejectMutation.isPending, disabled: !rejectNote.trim() }} title={t('reject')}>
        <Input.TextArea rows={4} value={rejectNote} onChange={(event) => setRejectNote(event.target.value)} placeholder={t('rejection_reason')} />
      </Modal>
      <Modal open={Boolean(selectedRecord)} onCancel={() => setDetailId(null)} footer={null} title={t('owner_registration_details')}>
        {selectedRecord ? (
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('business')}>{selectedRecord.businessName}</Descriptions.Item>
            <Descriptions.Item label={t('address')}>{selectedRecord.businessAddress}</Descriptions.Item>
            <Descriptions.Item label={t('phone')}>{selectedRecord.phoneNumber}</Descriptions.Item>
            <Descriptions.Item label={t('description')}>{selectedRecord.description ?? '--'}</Descriptions.Item>
            <Descriptions.Item label={t('status')}><StatusBadge value={selectedRecord.status} /></Descriptions.Item>
            <Descriptions.Item label={t('admin_note')}>{selectedRecord.adminNote ?? '--'}</Descriptions.Item>
          </Descriptions>
        ) : null}
      </Modal>
    </PageContainer>
  );
}
