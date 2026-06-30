import { useMutation, useQuery } from '@tanstack/react-query';
import { App } from 'antd';
import { useNavigate, useParams } from 'react-router-dom';
import { categoryApi } from '../api/categoryApi';
import { poiApi } from '../api/poiApi';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { PoiForm } from '../components/forms/PoiForm';
import { useI18n } from '../i18n/provider';
import { PageContainer } from '../components/layout/PageContainer';
import type { UpdatePoiRequest } from '../types/requests';

function normalizePriceRange(priceRange: string): '$' | '$$' | '$$$' {
  return priceRange === '$$' || priceRange === '$$$' ? priceRange : '$';
}

export function PoiEditPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const navigate = useNavigate();
  const { id = '' } = useParams();
  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: categoryApi.getAll });
  const poiQuery = useQuery({ queryKey: ['poi', id], queryFn: () => poiApi.getById(id), enabled: Boolean(id) });
  const updateMutation = useMutation({
    mutationFn: (payload: Parameters<typeof poiApi.update>[1]) => poiApi.update(id, payload),
    onSuccess: () => {
      notification.success({ message: t('pois_updated') });
      navigate(`/admin/pois/${id}`);
    },
    onError: (error: Error) => {
      notification.error({
        message: 'Cap nhat POI that bai',
        description: error.message,
      });
    },
  });

  const initialValues: UpdatePoiRequest | undefined = poiQuery.data
      ? {
          ...poiQuery.data,
          location: {
            latitude: poiQuery.data.latitude,
            longitude: poiQuery.data.longitude,
          },
          priceRange: normalizePriceRange(poiQuery.data.priceRange),
          mapUrl: poiQuery.data.mapUrl ?? undefined,
          ttsScript: poiQuery.data.ttsScript ?? undefined,
          activationRequested: false,
          autoTranslateAudioContent: true,
          overwriteAutoTranslations: false,
          autoTranslateLanguages: ['en', 'zh', 'ja', 'ko', 'fr', 'de', 'es', 'th', 'ru'],
        }
      : undefined;

  if (categoriesQuery.isLoading || poiQuery.isLoading || !poiQuery.data) return <LoadingScreen />;

  return (
    <PageContainer title={t('poi_edit_title')} subtitle={t('poi_edit_subtitle')}>
      <PoiForm
        categories={categoriesQuery.data ?? []}
        initialValues={initialValues}
        loading={updateMutation.isPending}
        onSubmit={async (values) => updateMutation.mutateAsync(values)}
      />
    </PageContainer>
  );
}
