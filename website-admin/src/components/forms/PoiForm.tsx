import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Card, Col, Form, Input, InputNumber, Row, Select, Space, Switch } from 'antd';
import { Controller, useForm } from 'react-hook-form';
import { useI18n } from '../../i18n/provider';
import type { CategoryResponse } from '../../types/responses';
import type { UpdatePoiRequest } from '../../types/requests';
import { PRICE_RANGES } from '../../utils/constants';
import { poiSchema } from '../../utils/validators';

type PoiFormValues = {
  name: string;
  description: string;
  categoryId: string;
  address: string;
  ward: string;
  district: string;
  city: string;
  latitude: number;
  longitude: number;
  priceRange: '$' | '$$' | '$$$';
  priority: number;
  mapUrl?: string;
  ttsScript?: string;
  geofenceRadiusMeters: number;
  autoNarrationEnabled: boolean;
  tagsText?: string;
  ownerId?: string;
  isActive: boolean;
  activationRequested: boolean;
};

interface PoiFormProps {
  categories: CategoryResponse[];
  initialValues?: UpdatePoiRequest;
  loading?: boolean;
  onSubmit: (values: UpdatePoiRequest) => Promise<unknown>;
}

function toFormValues(values?: UpdatePoiRequest): PoiFormValues {
  return {
    name: values?.name ?? '',
    description: values?.description ?? '',
    categoryId: values?.categoryId ?? '',
    address: values?.address ?? '',
    ward: values?.ward ?? '',
    district: values?.district ?? 'Quan 4',
    city: values?.city ?? 'TP.HCM',
    latitude: values?.location.latitude ?? 10.759,
    longitude: values?.location.longitude ?? 106.707,
    priceRange: values?.priceRange ?? '$',
    priority: values?.priority ?? 0,
    mapUrl: values?.mapUrl ?? '',
    ttsScript: values?.ttsScript ?? '',
    geofenceRadiusMeters: values?.geofenceRadiusMeters ?? 100,
    autoNarrationEnabled: values?.autoNarrationEnabled ?? true,
    tagsText: values?.tags.join(', ') ?? '',
    ownerId: values?.ownerId ?? '',
    isActive: values?.isActive ?? true,
    activationRequested: values?.activationRequested ?? false,
  };
}

export function PoiForm({ categories, initialValues, loading, onSubmit }: PoiFormProps) {
  const { t } = useI18n();
  const isEdit = Boolean(initialValues);
  const { control, handleSubmit } = useForm<PoiFormValues>({
    resolver: zodResolver(poiSchema),
    defaultValues: toFormValues(initialValues),
  });

  return (
    <Form layout="vertical" onFinish={handleSubmit(async (values) => {
      const payload: UpdatePoiRequest = {
        name: values.name,
        description: values.description,
        categoryId: values.categoryId,
        location: {
          latitude: values.latitude,
          longitude: values.longitude,
        },
        address: values.address,
        ward: values.ward,
        district: values.district,
        city: values.city,
        priceRange: values.priceRange,
        priority: values.priority,
        mapUrl: values.mapUrl?.trim() || undefined,
        ttsScript: values.ttsScript?.trim() || undefined,
        geofenceRadiusMeters: values.geofenceRadiusMeters,
        autoNarrationEnabled: values.autoNarrationEnabled,
        images: initialValues?.images ?? [],
        openingHours: initialValues?.openingHours ?? [],
        contactInfo: initialValues?.contactInfo ?? null,
        ownerId: values.ownerId || null,
        tags: values.tagsText?.split(',').map((item) => item.trim()).filter(Boolean) ?? [],
        isActive: values.isActive,
        activationRequested: values.activationRequested,
      };
      await onSubmit(payload);
    })}>
      <Card className="glass-card">
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Controller name="name" control={control} render={({ field, fieldState }) => <Form.Item label={t('name')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={12}>
            <Controller name="categoryId" control={control} render={({ field, fieldState }) => (
              <Form.Item label={t('category')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}>
                <Select {...field} options={categories.map((item) => ({ value: item.id, label: item.name }))} />
              </Form.Item>
            )} />
          </Col>
          <Col span={24}>
            <Controller name="description" control={control} render={({ field, fieldState }) => <Form.Item label={t('description')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input.TextArea rows={4} {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={12}>
            <Controller name="address" control={control} render={({ field, fieldState }) => <Form.Item label={t('address')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={12}>
            <Controller name="ward" control={control} render={({ field, fieldState }) => <Form.Item label={t('ward')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={8}>
            <Controller name="district" control={control} render={({ field }) => <Form.Item label={t('district')}><Input {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={8}>
            <Controller name="city" control={control} render={({ field }) => <Form.Item label={t('city')}><Input {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={8}>
            <Controller name="priceRange" control={control} render={({ field }) => (
              <Form.Item label={t('price')}>
                <Select {...field} options={PRICE_RANGES.map((item) => ({ value: item, label: item }))} />
              </Form.Item>
            )} />
          </Col>
          <Col xs={24} md={6}>
            <Controller name="latitude" control={control} render={({ field }) => <Form.Item label={t('latitude')}><InputNumber style={{ width: '100%' }} value={field.value} onChange={(value) => field.onChange(value ?? 0)} /></Form.Item>} />
          </Col>
          <Col xs={24} md={6}>
            <Controller name="longitude" control={control} render={({ field }) => <Form.Item label={t('longitude')}><InputNumber style={{ width: '100%' }} value={field.value} onChange={(value) => field.onChange(value ?? 0)} /></Form.Item>} />
          </Col>
          <Col xs={24} md={6}>
            <Controller name="priority" control={control} render={({ field }) => <Form.Item label={t('priority')}><InputNumber min={0} style={{ width: '100%' }} value={field.value} onChange={(value) => field.onChange(value ?? 0)} /></Form.Item>} />
          </Col>
          <Col xs={24} md={6}>
            <Controller name="geofenceRadiusMeters" control={control} render={({ field }) => <Form.Item label={t('geofence_radius')}><InputNumber min={10} max={10000} style={{ width: '100%' }} value={field.value} onChange={(value) => field.onChange(value ?? 100)} /></Form.Item>} />
          </Col>
          <Col span={24}>
            <Controller name="mapUrl" control={control} render={({ field }) => <Form.Item label={t('map_url')}><Input {...field} /></Form.Item>} />
          </Col>
          <Col span={24}>
            <Controller name="ttsScript" control={control} render={({ field }) => <Form.Item label={t('tts_script')}><Input.TextArea rows={4} {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={6}>
            <Controller name="ownerId" control={control} render={({ field }) => <Form.Item label={t('owner_id')}><Input {...field} /></Form.Item>} />
          </Col>
          <Col span={24}>
            <Controller name="tagsText" control={control} render={({ field }) => <Form.Item label={t('tags_comma')}><Input {...field} /></Form.Item>} />
          </Col>
          <Col xs={24} md={12}>
            <Controller name="isActive" control={control} render={({ field }) => <Form.Item label={t('active')}><Switch checked={field.value} onChange={field.onChange} /></Form.Item>} />
          </Col>
          <Col xs={24} md={12}>
            <Controller name="activationRequested" control={control} render={({ field }) => <Form.Item label={t('activation_requested')}><Switch checked={field.value} onChange={field.onChange} /></Form.Item>} />
          </Col>
          <Col xs={24} md={12}>
            <Controller name="autoNarrationEnabled" control={control} render={({ field }) => <Form.Item label={t('auto_narration_enabled')}><Switch checked={field.value} onChange={field.onChange} /></Form.Item>} />
          </Col>
        </Row>
      </Card>
      <Space style={{ marginTop: 16 }}>
        <Button type="primary" htmlType="submit" loading={loading}>
          {isEdit ? t('update_poi') : t('create_poi')}
        </Button>
      </Space>
    </Form>
  );
}
