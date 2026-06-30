import type { Lang } from '../types/responses';

export const LANGUAGE_OPTIONS: Array<{ value: Lang; label: string }> = [
  { value: 'vi', label: 'Tieng Viet' },
  { value: 'en', label: 'English' },
  { value: 'zh', label: 'Chinese' },
  { value: 'ja', label: 'Japanese' },
  { value: 'ko', label: 'Korean' },
];

export const UI_LANGUAGE_OPTIONS = LANGUAGE_OPTIONS.filter((option) => option.value === 'vi' || option.value === 'en');
