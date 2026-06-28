import { getData } from './axiosClient';
import type { QrActivationResponse } from '../types/responses';

export const qrApi = {
  resolve: (code: string) =>
    getData<QrActivationResponse>('/api/v1/qr-activations/resolve', { code }),
};
