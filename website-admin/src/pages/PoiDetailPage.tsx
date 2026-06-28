import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Descriptions, Image, Modal, Space, Table, Typography, Upload } from 'antd';
import { Clock3, Globe, MapPin, Music4, Phone, UploadCloud } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { audioApi } from '../api/audioApi';
import { localizationApi } from '../api/localizationApi';
import { mediaApi } from '../api/mediaApi';
import { poiApi } from '../api/poiApi';
import { StatusBadge } from '../components/common/StatusBadge';
import { AudioForm } from '../components/forms/AudioForm';
import { LocalizationForm } from '../components/forms/LocalizationForm';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { useI18n } from '../i18n/provider';
import { PageContainer } from '../components/layout/PageContainer';
import type { CreateLocalizationRequest, UpdatePoiRequest } from '../types/requests';
import { getMediaUrlOrPlaceholder, normalizeMediaUrl } from '../utils/media';

function normalizePriceRange(priceRange: string): '$' | '$$' | '$$$' {
  return priceRange === '$$' || priceRange === '$$$' ? priceRange : '$';
}

export function PoiDetailPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [audioModalOpen, setAudioModalOpen] = useState(false);
  const [localizationModalOpen, setLocalizationModalOpen] = useState(false);

  const poiQuery = useQuery({ queryKey: ['poi', id], queryFn: () => poiApi.getById(id), enabled: Boolean(id) });
  const audioQuery = useQuery({ queryKey: ['poi-audio', id], queryFn: () => audioApi.getPoiAudio(id), enabled: Boolean(id) });
  const localizationsQuery = useQuery({ queryKey: ['poi-localizations', id], queryFn: () => localizationApi.getByPoi(id), enabled: Boolean(id) });

  const updatePoiMutation = useMutation({
    mutationFn: (payload: Parameters<typeof poiApi.update>[1]) => poiApi.update(id, payload),
    onSuccess: () => {
      notification.success({ message: t('pois_updated') });
      queryClient.invalidateQueries({ queryKey: ['poi', id] });
    },
  });

  const uploadImageMutation = useMutation({
    mutationFn: mediaApi.uploadImage,
    onSuccess: async (file) => {
      await updatePoiMutation.mutateAsync({
        ...updatePayload,
        activationRequested: false,
        images: [...updatePayload.images, { url: file.url, caption: file.originalFileName, isThumbnail: updatePayload.images.length === 0 }],
      });
    },
  });

  const uploadAudioMutation = useMutation({
    mutationFn: ({ payload, file }: { payload: Parameters<typeof audioApi.uploadPoiAudio>[1]; file?: File }) =>
      audioApi.uploadPoiAudio(id, payload, file),
    onSuccess: () => {
      notification.success({ message: t('audio_saved') });
      setAudioModalOpen(false);
      queryClient.invalidateQueries({ queryKey: ['poi-audio', id] });
      queryClient.invalidateQueries({ queryKey: ['poi', id] });
    },
  });

  const localizationMutation = useMutation({
    mutationFn: ({ poiId, payload }: { poiId: string; payload: CreateLocalizationRequest }) => localizationApi.create(poiId, payload),
    onSuccess: () => {
      notification.success({ message: t('localization_created') });
      setLocalizationModalOpen(false);
      queryClient.invalidateQueries({ queryKey: ['poi-localizations', id] });
    },
  });

  const thumbnail = useMemo(() => poiQuery.data?.images.find((item) => item.isThumbnail) ?? poiQuery.data?.images[0], [poiQuery.data?.images]);

  if (poiQuery.isLoading || !poiQuery.data) return <LoadingScreen />;

  const updatePayload: UpdatePoiRequest = {
    ...poiQuery.data,
    location: { latitude: poiQuery.data.latitude, longitude: poiQuery.data.longitude },
    priceRange: normalizePriceRange(poiQuery.data.priceRange),
    mapUrl: poiQuery.data.mapUrl ?? undefined,
    ttsScript: poiQuery.data.ttsScript ?? undefined,
    activationRequested: false,
  };

  return (
    <PageContainer
      title={poiQuery.data.name}
      subtitle={t('poi_detail_subtitle')}
      extra={
        <Space wrap className="page-toolbar">
          <Button onClick={() => navigate(`/admin/pois/${id}/edit`)}>{t('edit')}</Button>
          <Button onClick={() => setAudioModalOpen(true)}>{t('upload_audio')}</Button>
          <Button onClick={() => setLocalizationModalOpen(true)}>{t('add_localization')}</Button>
        </Space>
      }
    >
      <div className="detail-grid">
        <Card className="glass-card detail-hero-card" bodyStyle={{ padding: 18 }}>
          <div className="detail-hero-banner">
            <Image
              preview={Boolean(thumbnail?.url)}
              src={getMediaUrlOrPlaceholder(thumbnail?.url)}
              fallback={getMediaUrlOrPlaceholder()}
              className="hero-image"
            />
            <div className="detail-hero-content">
              <Space wrap size="small">
                <span className="pill-badge">{poiQuery.data.priceRange || 'N/A'}</span>
                <StatusBadge value={poiQuery.data.isActive} trueLabel={t('active')} falseLabel={t('inactive')} />
                <span className="pill-badge">{poiQuery.data.audioStatus || 'audio'}</span>
              </Space>
              <Typography.Title level={2} style={{ color: '#fff', marginTop: 16, marginBottom: 8 }}>
                {poiQuery.data.name}
              </Typography.Title>
              <Typography.Paragraph className="hero-note" style={{ marginBottom: 0 }}>
                {poiQuery.data.address}, {poiQuery.data.ward}, {poiQuery.data.district}
              </Typography.Paragraph>
            </div>
          </div>
          <div className="info-list" style={{ marginTop: 18 }}>
            <div className="info-list-item"><span>{t('category_id')}</span><strong>{poiQuery.data.categoryId}</strong></div>
            <div className="info-list-item"><span>{t('coordinates')}</span><strong>{poiQuery.data.latitude}, {poiQuery.data.longitude}</strong></div>
            <div className="info-list-item"><span>{t('geofence_radius')}</span><strong>{poiQuery.data.geofenceRadiusMeters} m</strong></div>
            <div className="info-list-item"><span>{t('map_url')}</span><strong>{poiQuery.data.mapUrl ?? '--'}</strong></div>
            <div className="info-list-item"><span>{t('tags')}</span><strong>{poiQuery.data.tags.join(', ') || '--'}</strong></div>
          </div>
          <Typography.Title level={5} style={{ marginTop: 20 }}>{t('description')}</Typography.Title>
          <Typography.Paragraph>{poiQuery.data.description}</Typography.Paragraph>
          <Typography.Title level={5} style={{ marginTop: 20 }}>{t('tts_script')}</Typography.Title>
          <Typography.Paragraph>{poiQuery.data.ttsScript || '--'}</Typography.Paragraph>
        </Card>
        <div className="detail-side-stack">
          <Card className="glass-card" title={<Space><MapPin size={16} />Thông tin</Space>}>
            <Descriptions column={1} size="small">
              <Descriptions.Item label={t('address')}>{`${poiQuery.data.address}, ${poiQuery.data.ward}, ${poiQuery.data.district}`}</Descriptions.Item>
              <Descriptions.Item label={t('price')}>{poiQuery.data.priceRange}</Descriptions.Item>
              <Descriptions.Item label={t('rating')}>{poiQuery.data.rating}</Descriptions.Item>
              <Descriptions.Item label={t('priority')}>{poiQuery.data.priority}</Descriptions.Item>
              <Descriptions.Item label={t('auto_narration_enabled')}>
                <StatusBadge value={poiQuery.data.autoNarrationEnabled} trueLabel={t('yes')} falseLabel={t('no')} />
              </Descriptions.Item>
            </Descriptions>
          </Card>
          <Card className="glass-card" title={<Space><Music4 size={16} />{t('audio_title')}</Space>}>
            {audioQuery.data?.audioUrl ? (
              <audio controls src={normalizeMediaUrl(audioQuery.data.audioUrl)} className="detail-audio-player" />
            ) : (
              <Typography.Text type="secondary">{t('no_audio_uploaded')}</Typography.Text>
            )}
            <div style={{ marginTop: 16 }}>
              <Button onClick={() => setAudioModalOpen(true)} loading={uploadAudioMutation.isPending}>
                {t('upload_audio')}
              </Button>
            </div>
          </Card>
          <Card className="glass-card" title={<Space><Globe size={16} />Liên hệ</Space>}>
            <Space direction="vertical" size="small">
              <Typography.Text><Phone size={14} /> {poiQuery.data.contactInfo?.phone ?? '--'}</Typography.Text>
              <Typography.Text><Globe size={14} /> {poiQuery.data.contactInfo?.websiteUrl ?? '--'}</Typography.Text>
              <Typography.Text>Facebook: {poiQuery.data.contactInfo?.facebookUrl ?? '--'}</Typography.Text>
            </Space>
          </Card>
        </div>
      </div>
      <Card className="glass-card" title={<Space><Clock3 size={16} />Opening hours</Space>}>
        <Table
          className="table-responsive"
          rowKey={(record) => `${record.dayOfWeek}-${record.openTime}`}
          dataSource={poiQuery.data.openingHours}
          scroll={{ x: 760 }}
          pagination={false}
          columns={[
            { title: 'Day', dataIndex: 'dayOfWeek' },
            { title: 'Open', dataIndex: 'openTime' },
            { title: 'Close', dataIndex: 'closeTime' },
            { title: 'Status', render: (_, record) => <StatusBadge value={record.isClosed ? 'inactive' : 'active'} trueLabel={t('active')} falseLabel={t('inactive')} /> },
          ]}
        />
      </Card>
      <Card className="glass-card" title={t('images')}>
        <div className="gallery-grid">
          {poiQuery.data.images.map((image) => (
            <Image key={image.url} src={getMediaUrlOrPlaceholder(image.url)} fallback={getMediaUrlOrPlaceholder()} style={{ objectFit: 'cover', borderRadius: 16 }} />
          ))}
        </div>
        <div style={{ marginTop: 18 }}>
          <Upload beforeUpload={(file) => { uploadImageMutation.mutate(file); return false; }} showUploadList={false}>
            <Button icon={<UploadCloud size={16} />} loading={uploadImageMutation.isPending}>{t('upload_image')}</Button>
          </Upload>
        </div>
      </Card>
      <Card className="glass-card" title={t('localizations')}>
        <Table
          className="table-responsive"
          rowKey="id"
          dataSource={localizationsQuery.data ?? []}
          scroll={{ x: 960 }}
              columns={[
                { title: t('language'), dataIndex: 'lang' },
                { title: t('name'), dataIndex: 'name' },
                { title: t('description'), dataIndex: 'description' },
                { title: t('tts_script'), dataIndex: 'ttsScript' },
                { title: t('fallback'), render: (_, record) => <StatusBadge value={record.isFallback ? 'active' : 'inactive'} trueLabel={t('yes')} falseLabel={t('no')} /> },
              ]}
          pagination={false}
        />
      </Card>
      <Modal open={audioModalOpen} onCancel={() => setAudioModalOpen(false)} footer={null} title={t('upload_audio')} destroyOnClose>
        <AudioForm loading={uploadAudioMutation.isPending} onSubmit={async (payload, file) => uploadAudioMutation.mutateAsync({ payload, file })} />
      </Modal>
      <Modal open={localizationModalOpen} onCancel={() => setLocalizationModalOpen(false)} footer={null} title={t('add_localization')} destroyOnClose>
        <LocalizationForm loading={localizationMutation.isPending} onSubmit={async (payload) => localizationMutation.mutateAsync({ poiId: id, payload })} />
      </Modal>
    </PageContainer>
  );
}
