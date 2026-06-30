import { getData } from './axiosClient';
import type { NearbyPoi, Poi, PoiDetail } from '../types/responses';

export interface PoiQueryParams {
  lang?: string;
  audioLang?: string;
  keyword?: string;
  categoryId?: string;
  priceRange?: string;
}

export const poiApi = {
  list: (params?: PoiQueryParams) => getData<Poi[]>('/api/v1/poi/load-all', params),
  search: (params?: PoiQueryParams) => getData<Poi[]>('/api/v1/poi/search', params),
  detail: (id: string, lang: string, audioLang?: string) => getData<PoiDetail>(`/api/v1/poi/${id}`, { lang, audioLang }),
  nearby: (params: PoiQueryParams & { lat: number; lng: number; radius?: number; limit?: number }) =>
    getData<NearbyPoi[]>('/api/v1/poi/nearby', params),
};
