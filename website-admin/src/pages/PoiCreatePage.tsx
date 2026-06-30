import { useMutation, useQuery } from '@tanstack/react-query';
import { App } from 'antd';
import { useNavigate } from 'react-router-dom';
import { categoryApi } from '../api/categoryApi';
import { poiApi } from '../api/poiApi';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { PoiForm } from '../components/forms/PoiForm';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';

export function PoiCreatePage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const navigate = useNavigate();
  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: categoryApi.getAll });
  const createMutation = useMutation({
    mutationFn: poiApi.create,
    onSuccess: (poi) => {
      notification.success({ message: t('pois_created') });
      navigate(`/admin/pois/${poi.id}`);
    },
    onError: (error: Error) => {
      notification.error({
        message: 'Tao POI that bai',
        description: error.message,
      });
    },
  });

  if (categoriesQuery.isLoading) return <LoadingScreen />;

  return (
    <PageContainer title={t('poi_create_title')} subtitle={t('poi_create_subtitle')}>
      <PoiForm categories={categoriesQuery.data ?? []} loading={createMutation.isPending} onSubmit={async (values) => createMutation.mutateAsync(values)} />
    </PageContainer>
  );
}
