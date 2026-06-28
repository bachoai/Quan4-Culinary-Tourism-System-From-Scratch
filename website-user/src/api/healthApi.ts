import { getData } from './axiosClient';
import type { HealthResponse } from '../types/responses';

export const healthApi = {
  check: () => getData<HealthResponse>('/api/health'),
};
