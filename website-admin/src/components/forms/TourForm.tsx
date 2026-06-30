import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Form, Input, InputNumber, Select, Switch } from 'antd';
import { Controller, useForm } from 'react-hook-form';
import { useI18n } from '../../i18n/provider';
import type { CreateTourRequest, TourStopRequest, UpdateTourRequest } from '../../types/requests';
import type { TourResponse } from '../../types/responses';
import { SUPPORTED_LANGUAGES } from '../../utils/constants';
import { tourSchema } from '../../utils/validators';

type TourFormValues = {
  title: string;
  description: string;
  lang: string;
  coverImageUrl?: string;
  estimatedDurationMinutes: number;
  isActive: boolean;
  stopsText: string;
};

interface TourFormProps {
  initialValues?: TourResponse | null;
  loading?: boolean;
  onSubmit: (values: CreateTourRequest | UpdateTourRequest) => Promise<unknown>;
}

function toStopsText(stops?: TourResponse['stops']) {
  return (stops ?? [])
    .slice()
    .sort((left, right) => left.order - right.order)
    .map((stop) => [stop.poiId, stop.title ?? '', stop.estimatedStayMinutes].join('|'))
    .join('\n');
}

function parseStops(stopsText: string): TourStopRequest[] {
  return stopsText
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line, index) => {
      const [poiId, title, estimatedStayMinutes] = line.split('|');
      return {
        poiId: poiId.trim(),
        title: title?.trim() || undefined,
        order: index,
        estimatedStayMinutes: Number.parseInt(estimatedStayMinutes?.trim() || '15', 10) || 15,
      };
    });
}

export function TourForm({ initialValues, loading, onSubmit }: TourFormProps) {
  const { t } = useI18n();
  const { control, handleSubmit } = useForm<TourFormValues>({
    resolver: zodResolver(tourSchema),
    defaultValues: {
      title: initialValues?.title ?? '',
      description: initialValues?.description ?? '',
      lang: initialValues?.lang ?? 'vi',
      coverImageUrl: initialValues?.coverImageUrl ?? '',
      estimatedDurationMinutes: initialValues?.estimatedDurationMinutes ?? 60,
      isActive: initialValues?.isActive ?? true,
      stopsText: toStopsText(initialValues?.stops),
    },
  });

  return (
    <Form
      layout="vertical"
      onFinish={handleSubmit(async (values) => {
        await onSubmit({
          title: values.title,
          description: values.description,
          lang: values.lang,
          coverImageUrl: values.coverImageUrl?.trim() || undefined,
          estimatedDurationMinutes: values.estimatedDurationMinutes,
          isActive: values.isActive,
          stops: parseStops(values.stopsText),
        });
      })}
    >
      <Controller name="title" control={control} render={({ field, fieldState }) => <Form.Item label={t('name')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input {...field} /></Form.Item>} />
      <Controller name="description" control={control} render={({ field, fieldState }) => <Form.Item label={t('description')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input.TextArea rows={4} {...field} /></Form.Item>} />
      <Controller name="lang" control={control} render={({ field }) => <Form.Item label={t('language')}><Select {...field} options={SUPPORTED_LANGUAGES.map((value) => ({ value, label: value.toUpperCase() }))} /></Form.Item>} />
      <Controller name="coverImageUrl" control={control} render={({ field }) => <Form.Item label={t('cover_image_url')}><Input {...field} /></Form.Item>} />
      <Controller name="estimatedDurationMinutes" control={control} render={({ field }) => <Form.Item label={t('tour_duration_minutes')}><InputNumber min={1} max={1440} style={{ width: '100%' }} value={field.value} onChange={(value) => field.onChange(value ?? 60)} /></Form.Item>} />
      <Controller name="isActive" control={control} render={({ field }) => <Form.Item label={t('active')}><Switch checked={field.value} onChange={field.onChange} /></Form.Item>} />
      <Controller
        name="stopsText"
        control={control}
        render={({ field, fieldState }) => (
          <Form.Item
            label={t('tour_stops')}
            validateStatus={fieldState.error ? 'error' : ''}
            help={fieldState.error?.message ?? t('tour_stops_hint')}
          >
            <Input.TextArea rows={6} {...field} />
          </Form.Item>
        )}
      />
      <Button type="primary" htmlType="submit" loading={loading} block>
        {initialValues ? t('update_tour') : t('create_tour')}
      </Button>
    </Form>
  );
}
