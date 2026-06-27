import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Descriptions, Input, Modal, Select, Space, Table } from 'antd';
import { useMemo, useState } from 'react';
import { ownerApi } from '../api/ownerApi';
import { useI18n } from '../i18n/provider';
import { StatusBadge } from '../components/common/StatusBadge';
import { PageContainer } from '../components/layout/PageContainer';

export function OwnerSubmissionPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<string | undefined>();
  const [submissionType, setSubmissionType] = useState<string | undefined>();
  const [rejectId, setRejectId] = useState<string | null>(null);
  const [note, setNote] = useState('');
  const [detailId, setDetailId] = useState<string | null>(null);

  const query = useQuery({ queryKey: ['submissions', status], queryFn: () => ownerApi.getSubmissions(status) });
  const approveMutation = useMutation({
    mutationFn: (id: string) => ownerApi.approveSubmission(id, {}),
    onSuccess: () => {
      notification.success({ message: t('submission_approved') });
      queryClient.invalidateQueries({ queryKey: ['submissions'] });
    },
  });
  const rejectMutation = useMutation({
    mutationFn: ({ id, adminNote }: { id: string; adminNote: string }) => ownerApi.rejectSubmission(id, { adminNote }),
    onSuccess: () => {
      notification.success({ message: t('submission_rejected') });
      setRejectId(null);
      setNote('');
      queryClient.invalidateQueries({ queryKey: ['submissions'] });
    },
  });

  const data = useMemo(() => {
    if (!submissionType) return query.data ?? [];
    return (query.data ?? []).filter((item) => item.submissionType === submissionType);
  }, [query.data, submissionType]);

  const selectedRecord = data.find((item) => item.id === detailId) ?? null;

  return (
    <PageContainer title={t('submissions_title')} subtitle={t('submissions_subtitle')}>
      <Card className="glass-card">
        <Space style={{ marginBottom: 16 }}>
          <Select allowClear placeholder={t('status')} style={{ width: 160 }} options={[{ value: 'pending', label: t('pending') }, { value: 'approved', label: t('approved') }, { value: 'rejected', label: t('rejected') }]} onChange={setStatus} />
          <Select allowClear placeholder={t('submission_type')} style={{ width: 180 }} options={[{ value: 'create', label: t('type_create') }, { value: 'update', label: t('type_update') }]} onChange={setSubmissionType} />
        </Space>
        <Table
          className="table-responsive"
          rowKey="id"
          dataSource={data}
          loading={query.isFetching}
          columns={[
            { title: t('name'), dataIndex: 'poiName' },
            { title: t('submission_type'), dataIndex: 'submissionType', render: (value) => <StatusBadge value={value} /> },
            { title: 'POI ID', dataIndex: 'poiId' },
            { title: t('status'), render: (_, record) => <StatusBadge value={record.status} /> },
            {
              title: t('actions'),
              render: (_, record) => (
                <Space>
                  <Button onClick={() => setDetailId(record.id)}>Chi tiết</Button>
                  <Button type="primary" onClick={() => Modal.confirm({ title: 'Approve submission?', content: record.poiName, onOk: () => approveMutation.mutate(record.id) })}>{t('approve')}</Button>
                  <Button danger onClick={() => setRejectId(record.id)}>{t('reject')}</Button>
                </Space>
              ),
            },
          ]}
        />
      </Card>
      <Modal open={Boolean(rejectId)} onCancel={() => setRejectId(null)} onOk={() => rejectId && note.trim() && rejectMutation.mutate({ id: rejectId, adminNote: note.trim() })} okButtonProps={{ danger: true, loading: rejectMutation.isPending, disabled: !note.trim() }} title={t('reject')}>
        <Input.TextArea rows={4} value={note} onChange={(event) => setNote(event.target.value)} placeholder={t('rejection_reason')} />
      </Modal>
      <Modal open={Boolean(selectedRecord)} onCancel={() => setDetailId(null)} footer={null} title="Submission detail">
        {selectedRecord ? (
          <Descriptions column={1} size="small">
            <Descriptions.Item label="POI">{selectedRecord.poiName}</Descriptions.Item>
            <Descriptions.Item label="POI ID">{selectedRecord.poiId ?? '--'}</Descriptions.Item>
            <Descriptions.Item label="Type"><StatusBadge value={selectedRecord.submissionType} /></Descriptions.Item>
            <Descriptions.Item label="Status"><StatusBadge value={selectedRecord.status} /></Descriptions.Item>
            <Descriptions.Item label={t('priority')}>{selectedRecord.priority}</Descriptions.Item>
            <Descriptions.Item label={t('map_url')}>{selectedRecord.mapUrl ?? '--'}</Descriptions.Item>
            <Descriptions.Item label={t('tts_script')}>{selectedRecord.ttsScript ?? '--'}</Descriptions.Item>
            <Descriptions.Item label={t('geofence_radius')}>{selectedRecord.geofenceRadiusMeters} m</Descriptions.Item>
            <Descriptions.Item label={t('auto_narration_enabled')}>
              <StatusBadge value={selectedRecord.autoNarrationEnabled} trueLabel={t('yes')} falseLabel={t('no')} />
            </Descriptions.Item>
            <Descriptions.Item label="Admin note">{selectedRecord.adminNote ?? '--'}</Descriptions.Item>
          </Descriptions>
        ) : null}
      </Modal>
    </PageContainer>
  );
}
