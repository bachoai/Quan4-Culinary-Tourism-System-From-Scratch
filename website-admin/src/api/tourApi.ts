import axiosClient from './axiosClient';
import type { CreateTourRequest, UpdateTourRequest } from '../types/requests';
import type { TourResponse } from '../types/responses';

export const tourApi = {
  getAll: () => axiosClient.get<never, TourResponse[]>('/api/v1/admin/tours'),
  getById: (id: string) => axiosClient.get<never, TourResponse>(`/api/v1/admin/tours/${id}`),
  create: (payload: CreateTourRequest) => axiosClient.post<never, TourResponse>('/api/v1/admin/tours', payload),
  update: (id: string, payload: UpdateTourRequest) => axiosClient.put<never, TourResponse>(`/api/v1/admin/tours/${id}`, payload),
  delete: (id: string) => axiosClient.delete(`/api/v1/admin/tours/${id}`),
};
