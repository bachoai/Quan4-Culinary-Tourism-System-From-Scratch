import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, App, Button, Card, Form, Input, Select, Space, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { audioApi } from '../api/audioApi';
import { localizationApi } from '../api/localizationApi';
import { poiApi } from '../api/poiApi';
import { ConfirmDeleteButton } from '../components/common/ConfirmDeleteButton';
import { EmptyState } from '../components/common/EmptyState';
import { AudioForm } from '../components/forms/AudioForm';
import { useI18n } from '../i18n/provider';
import { PageContainer } from '../components/layout/PageContainer';
import type { UpdateLocalizationRequest, UpdatePoiRequest } from '../types/requests';
import type { PoiDetailResponse } from '../types/responses';
import { SUPPORTED_LANGUAGES } from '../utils/constants';
import { normalizeMediaUrl } from '../utils/media';

type NarrationScriptFormValues = {
  ttsScript: string;
  voiceName: string;
};

function normalizePriceRange(priceRange: string): '$' | '$$' | '$$$' {
  return priceRange === '$$' || priceRange === '$$$' ? priceRange : '$';
}

function toPoiUpdatePayload(poi: PoiDetailResponse, ttsScript: string): UpdatePoiRequest {
  return {
    name: poi.name,
    description: poi.description,
    categoryId: poi.categoryId,
    location: { latitude: poi.latitude, longitude: poi.longitude },
    address: poi.address,
    ward: poi.ward,
    district: poi.district,
    city: poi.city,
    priceRange: normalizePriceRange(poi.priceRange),
    priority: poi.priority,
    mapUrl: poi.mapUrl ?? undefined,
    ttsScript: ttsScript.trim() || undefined,
    geofenceRadiusMeters: poi.geofenceRadiusMeters,
    autoNarrationEnabled: poi.autoNarrationEnabled,
    images: poi.images,
    openingHours: poi.openingHours,
    contactInfo: poi.contactInfo ?? null,
    ownerId: poi.ownerId ?? null,
    tags: poi.tags,
    isActive: poi.isActive,
    activationRequested: false,
    autoTranslateAudioContent: false,
    overwriteAutoTranslations: false,
    autoTranslateLanguages: SUPPORTED_LANGUAGES.filter((value) => value !== 'vi'),
  };
}

export function AudioPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<NarrationScriptFormValues>();
  const [poiId, setPoiId] = useState<string>();
  const [lang, setLang] = useState<string>('vi');

  const poisQuery = useQuery({ queryKey: ['pois'], queryFn: () => poiApi.loadAll() });
  const poiQuery = useQuery({ queryKey: ['audio-poi', poiId], queryFn: () => poiApi.getById(poiId!), enabled: Boolean(poiId) });
  const localizationsQuery = useQuery({
    queryKey: ['audio-localizations', poiId],
    queryFn: () => localizationApi.getByPoi(poiId!),
    enabled: Boolean(poiId),
  });
  const audioQuery = useQuery({
    queryKey: ['poi-audio', poiId, lang],
    queryFn: () => audioApi.getPoiAudio(poiId!, lang),
    enabled: Boolean(poiId),
  });
  const manifestQuery = useQuery({ queryKey: ['audio-manifest'], queryFn: audioApi.getPackManifest });

  const selectedLocalization =
    lang === 'vi' ? null : (localizationsQuery.data ?? []).find((item) => item.lang === lang) ?? null;
  const localizationRequired = Boolean(poiId) && lang !== 'vi' && !selectedLocalization && !localizationsQuery.isFetching;
  const currentScript = lang === 'vi' ? (poiQuery.data?.ttsScript ?? '') : (selectedLocalization?.ttsScript ?? '');
  const currentVoiceName = audioQuery.data?.voiceName ?? '';

  useEffect(() => {
    form.setFieldsValue({
      ttsScript: currentScript,
      voiceName: currentVoiceName,
    });
  }, [currentScript, currentVoiceName, form, lang, poiId]);

  const refreshWorkspace = async (selectedPoiId: string, selectedLang: string) => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['audio-poi', selectedPoiId] }),
      queryClient.invalidateQueries({ queryKey: ['poi', selectedPoiId] }),
      queryClient.invalidateQueries({ queryKey: ['audio-localizations', selectedPoiId] }),
      queryClient.invalidateQueries({ queryKey: ['localizations', selectedPoiId] }),
      queryClient.invalidateQueries({ queryKey: ['poi-localizations', selectedPoiId] }),
      queryClient.invalidateQueries({ queryKey: ['poi-audio', selectedPoiId, selectedLang] }),
      queryClient.invalidateQueries({ queryKey: ['poi-audio', selectedPoiId] }),
      queryClient.invalidateQueries({ queryKey: ['audio-manifest'] }),
    ]);
  };

  const saveAndGenerateMutation = useMutation({
    mutationFn: async (values: NarrationScriptFormValues) => {
      if (!poiId) {
        throw new Error(t('no_poi_selected_desc'));
      }

      const trimmedScript = values.ttsScript.trim();
      const trimmedVoiceName = values.voiceName.trim();

      if (lang === 'vi') {
        if (!poiQuery.data) {
          throw new Error(t('no_poi_selected_desc'));
        }

        await poiApi.update(poiId, toPoiUpdatePayload(poiQuery.data, trimmedScript));
      } else {
        if (!selectedLocalization) {
          throw new Error(t('localization_required_desc'));
        }

        const payload: UpdateLocalizationRequest = {
          lang: selectedLocalization.lang,
          name: selectedLocalization.name,
          description: selectedLocalization.description,
          audioUrl: selectedLocalization.audioUrl ?? undefined,
          ttsScript: trimmedScript || undefined,
          isFallback: selectedLocalization.isFallback,
        };
        await localizationApi.update(poiId, selectedLocalization.lang, payload);
      }

      return audioApi.generatePoiAudio(poiId, {
        lang,
        voiceName: trimmedVoiceName || undefined,
      });
    },
    onSuccess: async () => {
      if (!poiId) {
        return;
      }

      notification.success({ message: t('audio_generated') });
      await refreshWorkspace(poiId, lang);
    },
    onError: (error: Error) => {
      notification.error({
        message: t('audio_generation_failed'),
        description: error.message,
      });
    },
  });

  const uploadMutation = useMutation({
    mutationFn: ({ payload, file }: { payload: Parameters<typeof audioApi.uploadPoiAudio>[1]; file?: File }) =>
      audioApi.uploadPoiAudio(poiId!, payload, file),
    onSuccess: async () => {
      if (!poiId) {
        return;
      }

      notification.success({ message: t('audio_saved') });
      await refreshWorkspace(poiId, lang);
    },
    onError: (error: Error) => {
      notification.error({
        message: t('audio_save_failed'),
        description: error.message,
      });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async () => {
      if (!poiId) {
        throw new Error(t('no_poi_selected_desc'));
      }

      return audioApi.deletePoiAudio(poiId, lang);
    },
    onSuccess: async () => {
      if (!poiId) {
        return;
      }

      notification.success({ message: t('audio_deleted') });
      await refreshWorkspace(poiId, lang);
    },
    onError: (error: Error) => {
      notification.error({
        message: t('audio_delete_failed'),
        description: error.message,
      });
    },
  });

  const scriptEditorDisabled =
    !poiId ||
    poiQuery.isFetching ||
    localizationsQuery.isFetching ||
    localizationRequired ||
    saveAndGenerateMutation.isPending;

  return (
    <PageContainer title={t('audio_title')} subtitle={t('audio_subtitle')}>
      <Card className="glass-card">
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <Space wrap className="page-toolbar">
            <Select
              placeholder={t('choose_poi')}
              style={{ width: 320 }}
              options={(poisQuery.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
              onChange={setPoiId}
            />
            <Select
              value={lang}
              style={{ width: 140 }}
              options={SUPPORTED_LANGUAGES.map((value) => ({ value, label: value.toUpperCase() }))}
              onChange={setLang}
            />
          </Space>
          {!poiId ? (
            <EmptyState title={t('no_poi_selected')} description={t('no_poi_selected_desc')} />
          ) : (
            <>
              <Card type="inner" title={t('narration_script_title')}>
                {localizationRequired ? (
                  <Alert
                    type="warning"
                    showIcon
                    message={t('localization_required_title')}
                    description={t('localization_required_desc')}
                    style={{ marginBottom: 16 }}
                  />
                ) : null}
                <Form
                  form={form}
                  layout="vertical"
                  onFinish={async (values) => {
                    await saveAndGenerateMutation.mutateAsync(values);
                  }}
                >
                  <Form.Item name="ttsScript" label={t('tts_script')} extra={t('tts_script_hint')}>
                    <Input.TextArea rows={5} disabled={scriptEditorDisabled} />
                  </Form.Item>
                  <Form.Item name="voiceName" label={t('voice_name')}>
                    <Input disabled={scriptEditorDisabled} />
                  </Form.Item>
                  <Button
                    type="primary"
                    htmlType="submit"
                    loading={saveAndGenerateMutation.isPending}
                    disabled={scriptEditorDisabled}
                  >
                    {t('save_and_generate_audio')}
                  </Button>
                </Form>
              </Card>
              <Card
                type="inner"
                title={t('audio_preview_title')}
                extra={audioQuery.data?.audioUrl ? <ConfirmDeleteButton onConfirm={() => deleteMutation.mutate()} loading={deleteMutation.isPending} /> : null}
              >
                {audioQuery.data?.audioUrl ? (
                  <audio controls src={normalizeMediaUrl(audioQuery.data.audioUrl)} style={{ width: '100%' }} />
                ) : (
                  <Typography.Text type="secondary">{t('no_audio_found')}</Typography.Text>
                )}
              </Card>
              <Card type="inner" title={t('manual_audio_override_title')}>
                <Typography.Paragraph type="secondary">
                  {t('manual_audio_override_desc')}
                </Typography.Paragraph>
                <AudioForm
                  hideLanguage
                  loading={uploadMutation.isPending}
                  onSubmit={async (payload, file) => uploadMutation.mutateAsync({ payload: { ...payload, lang }, file })}
                />
              </Card>
            </>
          )}
        </Space>
      </Card>
      <Card className="glass-card" title={t('pack_manifest')}>
        {manifestQuery.data?.items?.length ? (
          <Typography.Paragraph>
            {t('manifest_entries')}: {manifestQuery.data.items.length}
          </Typography.Paragraph>
        ) : (
          <EmptyState title={t('manifest_empty')} description={t('manifest_empty_desc')} />
        )}
      </Card>
    </PageContainer>
  );
}
