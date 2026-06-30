import { getData, postData } from './axiosClient';
import type { CreateTourRequest } from '../types/requests';
import type { Lang, TourResponse } from '../types/responses';

export const tourApi = {
  list: (lang?: Lang) => getData<TourResponse[]>('/api/v1/tours', lang ? { lang } : undefined),
  listMine: () => getData<TourResponse[]>('/api/v1/tours/my'),
  createMine: (payload: CreateTourRequest) => postData<TourResponse>('/api/v1/tours/my', payload),
};
