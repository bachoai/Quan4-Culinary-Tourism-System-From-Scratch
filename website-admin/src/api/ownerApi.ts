import axiosClient from './axiosClient';
import type { RejectRequest } from '../types/requests';
import type { OwnerRegistrationAdminResponse, OwnerSubmissionResponse } from '../types/responses';

export const ownerApi = {
  getOwnerRegistrations: (status?: string) =>
    axiosClient.get<never, OwnerRegistrationAdminResponse[]>('/api/v1/admin/owner-registrations', { params: { status } }),
  approveOwner: (id: string) =>
    axiosClient.put(`/api/v1/admin/owner-registrations/${id}/approve`),
  rejectOwner: (id: string, payload: RejectRequest) =>
    axiosClient.put(`/api/v1/admin/owner-registrations/${id}/reject`, payload),
  disableOwner: (id: string) =>
    axiosClient.delete(`/api/v1/admin/owner-registrations/${id}/disable`),
  getSubmissions: (status?: string) =>
    axiosClient.get<never, OwnerSubmissionResponse[]>('/api/v1/admin/submissions', { params: { status } }),
  approveSubmission: (id: string) =>
    axiosClient.put(`/api/v1/admin/submissions/${id}/approve`),
  rejectSubmission: (id: string, payload: RejectRequest) =>
    axiosClient.put(`/api/v1/admin/submissions/${id}/reject`, payload),
};
