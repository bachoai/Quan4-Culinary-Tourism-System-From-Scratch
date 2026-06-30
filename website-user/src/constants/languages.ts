import type { Lang } from '../types/responses';

export const LANGUAGE_OPTIONS: Array<{ value: Lang; label: string }> = [
  { value: 'vi', label: 'Vietnamese' },
  { value: 'en', label: 'English' },
  { value: 'zh', label: 'Chinese' },
  { value: 'ja', label: 'Japanese' },
  { value: 'ko', label: 'Korean' },
  { value: 'fr', label: 'French' },
  { value: 'de', label: 'German' },
  { value: 'es', label: 'Spanish' },
  { value: 'th', label: 'Thai' },
  { value: 'ru', label: 'Russian' },
];

export const UI_LANGUAGE_OPTIONS = LANGUAGE_OPTIONS.filter((option) => option.value === 'vi' || option.value === 'en');
