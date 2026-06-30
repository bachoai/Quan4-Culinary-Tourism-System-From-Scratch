import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Form, Input, Select, Switch } from 'antd';
import { Controller, useForm } from 'react-hook-form';
import { useI18n } from '../../i18n/provider';
import type { CreateLocalizationRequest } from '../../types/requests';
import { SUPPORTED_LANGUAGES } from '../../utils/constants';
import { localizationSchema } from '../../utils/validators';

interface LocalizationFormProps {
  initialValues?: CreateLocalizationRequest;
  loading?: boolean;
  disableLanguage?: boolean;
  onSubmit: (values: CreateLocalizationRequest) => Promise<unknown>;
}

export function LocalizationForm({ initialValues, loading, disableLanguage, onSubmit }: LocalizationFormProps) {
  const { t } = useI18n();
  const { control, handleSubmit } = useForm<CreateLocalizationRequest>({
    resolver: zodResolver(localizationSchema),
      defaultValues: initialValues ?? {
        lang: 'en',
        name: '',
        description: '',
        audioUrl: '',
        ttsScript: '',
        isFallback: false,
      },
  });

  return (
    <Form layout="vertical" onFinish={handleSubmit(async (values) => onSubmit(values))}>
      <Controller
        name="lang"
        control={control}
        render={({ field }) => (
          <Form.Item label={t('language')}>
            <Select
              {...field}
              disabled={disableLanguage}
              options={SUPPORTED_LANGUAGES.map((value) => ({ value, label: value.toUpperCase() }))}
            />
          </Form.Item>
        )}
      />
      <Controller name="name" control={control} render={({ field, fieldState }) => <Form.Item label={t('name')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input {...field} /></Form.Item>} />
      <Controller name="description" control={control} render={({ field, fieldState }) => <Form.Item label={t('description')} validateStatus={fieldState.error ? 'error' : ''} help={fieldState.error?.message}><Input.TextArea rows={4} {...field} /></Form.Item>} />
      <Controller
        name="ttsScript"
        control={control}
        render={({ field }) => (
          <Form.Item label={t('tts_script')} extra={t('tts_script_hint')}>
            <Input.TextArea rows={3} {...field} />
          </Form.Item>
        )}
      />
      <Controller name="audioUrl" control={control} render={({ field }) => <Form.Item label={t('audio_url')}><Input {...field} /></Form.Item>} />
      <Controller name="isFallback" control={control} render={({ field }) => <Form.Item label={t('fallback')}><Switch checked={field.value} onChange={field.onChange} /></Form.Item>} />
      <Button type="primary" htmlType="submit" loading={loading} block>
        {t('save_localization')}
      </Button>
    </Form>
  );
}
