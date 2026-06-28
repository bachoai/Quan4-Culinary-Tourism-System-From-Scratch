import { getData, postData, putData } from './axiosClient';
import type {
  OwnerDashboardResponse,
  OwnerManagedPoi,
  OwnerSubmissionResponse,
} from '../types/responses';
import type { CreateOwnerSubmissionRequest } from '../types/requests';

export const ownerApi = {
  dashboard: () => getData<OwnerDashboardResponse>('/api/v1/owner/dashboard'),
  pois: (lang?: string) => getData<OwnerManagedPoi[]>('/api/v1/owner/pois', lang ? { lang } : undefined),
  submissions: () => getData<OwnerSubmissionResponse[]>('/api/v1/owner/submissions'),
  submissionById: (id: string) => getData<OwnerSubmissionResponse>(`/api/v1/owner/submissions/${id}`),
  createSubmission: (payload: CreateOwnerSubmissionRequest) =>
    postData<OwnerSubmissionResponse>('/api/v1/owner/submissions', payload),
  updateSubmission: (id: string, payload: CreateOwnerSubmissionRequest) =>
    putData<OwnerSubmissionResponse>(`/api/v1/owner/submissions/${id}`, payload),
};
