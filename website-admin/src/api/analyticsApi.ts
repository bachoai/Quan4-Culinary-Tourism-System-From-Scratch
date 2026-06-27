import axiosClient from './axiosClient';
import type { CollectAnalyticsRequest } from '../types/requests';
import type { AnalyticsSummaryResponse, UsageHistoryEntryResponse } from '../types/responses';
import type { PagedResponse } from '../types/common';

export const analyticsApi = {
  collect: (payload: CollectAnalyticsRequest) => axiosClient.post('/api/v1/analytics/collect', payload),
  summary: () => axiosClient.get<never, AnalyticsSummaryResponse>('/api/v1/admin/analytics/summary'),
  history: (params?: { page?: number; pageSize?: number; eventName?: string; poiId?: string; lang?: string }) =>
    axiosClient.get<never, PagedResponse<UsageHistoryEntryResponse>>('/api/v1/admin/analytics/history', { params }),
};
