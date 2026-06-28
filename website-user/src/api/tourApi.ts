import { getData } from './axiosClient';
import type { Lang, TourResponse } from '../types/responses';

export const tourApi = {
  list: (lang?: Lang) => getData<TourResponse[]>('/api/v1/tours', lang ? { lang } : undefined),
};
