import { useMutation, useQuery } from '@tanstack/react-query';
import { App, Card, Select, Space, Typography } from 'antd';
import { useState } from 'react';
import { audioApi } from '../api/audioApi';
import { poiApi } from '../api/poiApi';
import { AudioForm } from '../components/forms/AudioForm';
import { EmptyState } from '../components/common/EmptyState';
import { useI18n } from '../i18n/provider';
import { PageContainer } from '../components/layout/PageContainer';
import { normalizeMediaUrl } from '../utils/media';

export function AudioPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const [poiId, setPoiId] = useState<string>();
  const [lang, setLang] = useState<string>('vi');
  const poisQuery = useQuery({ queryKey: ['pois'], queryFn: () => poiApi.loadAll() });
  const audioQuery = useQuery({ queryKey: ['poi-audio', poiId, lang], queryFn: () => audioApi.getPoiAudio(poiId!, lang), enabled: Boolean(poiId) });
  const manifestQuery = useQuery({ queryKey: ['audio-manifest'], queryFn: audioApi.getPackManifest });
  const uploadMutation = useMutation({
    mutationFn: ({ payload, file }: { payload: Parameters<typeof audioApi.uploadPoiAudio>[1]; file?: File }) =>
      audioApi.uploadPoiAudio(poiId!, payload, file),
    onSuccess: () => {
      notification.success({ message: t('audio_saved') });
    },
  });

  return (
    <PageContainer title={t('audio_title')} subtitle={t('audio_subtitle')}>
      <Card className="glass-card">
        <Space wrap direction="vertical" style={{ width: '100%' }}>
          <Space wrap className="page-toolbar">
            <Select
              placeholder={t('choose_poi')}
              style={{ width: 320 }}
              options={(poisQuery.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
              onChange={setPoiId}
            />
            <Select
              value={lang}
              style={{ width: 120 }}
              options={['vi', 'en', 'zh', 'ja', 'ko'].map((value) => ({ value, label: value.toUpperCase() }))}
              onChange={setLang}
            />
          </Space>
          {poiId ? (
            <AudioForm loading={uploadMutation.isPending} onSubmit={async (payload, file) => uploadMutation.mutateAsync({ payload: { ...payload, lang }, file })} />
          ) : (
            <EmptyState title={t('no_poi_selected')} description={t('no_poi_selected_desc')} />
          )}
          {audioQuery.data?.audioUrl ? (
            <audio controls src={normalizeMediaUrl(audioQuery.data.audioUrl)} style={{ width: '100%' }} />
          ) : (
            <Typography.Text type="secondary">{t('no_audio_found')}</Typography.Text>
          )}
        </Space>
      </Card>
      <Card className="glass-card" title={t('pack_manifest')}>
        {manifestQuery.data?.items?.length ? (
          <Typography.Paragraph>{t('manifest_entries')}: {manifestQuery.data.items.length}</Typography.Paragraph>
        ) : (
          <EmptyState title={t('manifest_empty')} description={t('manifest_empty_desc')} />
        )}
      </Card>
    </PageContainer>
  );
}
