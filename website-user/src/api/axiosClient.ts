import axios from 'axios';
import type { AxiosError } from 'axios';
import { useAppStore } from '../store/appStore';
import type { ApiResponse } from '../types/responses';

const AUTH_TOKEN_KEY = 'q4-token';

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5163').replace(/\/$/, '');

const client = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
});

client.interceptors.request.use((config) => {
  const token = localStorage.getItem(AUTH_TOKEN_KEY);
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

function unwrap<T>(payload: ApiResponse<T>): T {
  if (!payload.success) {
    throw new Error(payload.message || 'Khong the xu ly yeu cau');
  }

  return payload.data;
}

function tryReadValidationMessage(payload: unknown): string | null {
  if (!payload || typeof payload !== 'object') {
    return null;
  }

  const record = payload as {
    title?: unknown;
    detail?: unknown;
    errors?: Record<string, unknown>;
  };

  const messages = Object.values(record.errors ?? {})
    .flatMap((value) => (Array.isArray(value) ? value : [value]))
    .filter((value): value is string => typeof value === 'string' && value.trim().length > 0);

  if (messages.length > 0) {
    return messages.join(' ');
  }

  if (typeof record.detail === 'string' && record.detail.trim()) {
    return record.detail;
  }

  if (typeof record.title === 'string' && record.title.trim()) {
    return record.title;
  }

  return null;
}

function toError(error: unknown): Error {
  const axiosError = error as AxiosError<ApiResponse<unknown>>;
  const status = axiosError.response?.status;
  const requestUrl = axiosError.config?.url || '';
  const hasStoredToken = Boolean(localStorage.getItem(AUTH_TOKEN_KEY));
  const isAuthEntryRequest =
    requestUrl.includes('/api/v1/auth/login') ||
    requestUrl.includes('/api/v1/auth/register');

  if (status === 401 && hasStoredToken && !isAuthEntryRequest) {
    useAppStore.getState().logout();

    if (typeof window !== 'undefined') {
      const currentHashRoute = window.location.hash.replace(/^#/, '') || '/';
      if (currentHashRoute.startsWith('/login')) {
        return new Error(
          axiosError.response?.data?.message ||
          axiosError.message ||
          'Phien dang nhap da het han.',
        );
      }

      const next = currentHashRoute;
      const encodedNext = encodeURIComponent(next || '/account');
      window.location.replace(`/#/login?next=${encodedNext}&reason=session-expired`);
    }
  }

  const validationMessage = tryReadValidationMessage(axiosError.response?.data);
  const message =
    axiosError.response?.data?.message ||
    validationMessage ||
    axiosError.message ||
    'Khong the ket noi den may chu';

  return new Error(message);
}

export async function getData<T>(url: string, params?: object): Promise<T> {
  try {
    const { data } = await client.get<ApiResponse<T>>(url, { params });
    return unwrap(data);
  } catch (error) {
    throw toError(error);
  }
}

export async function postData<T>(url: string, body: object): Promise<T> {
  try {
    const { data } = await client.post<ApiResponse<T>>(url, body);
    return unwrap(data);
  } catch (error) {
    throw toError(error);
  }
}

export async function postFormData<T>(url: string, formData: FormData): Promise<T> {
  try {
    const { data } = await client.post<ApiResponse<T>>(url, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return unwrap(data);
  } catch (error) {
    throw toError(error);
  }
}

export async function putData<T>(url: string, body: object): Promise<T> {
  try {
    const { data } = await client.put<ApiResponse<T>>(url, body);
    return unwrap(data);
  } catch (error) {
    throw toError(error);
  }
}

export async function deleteData<T>(url: string): Promise<T> {
  try {
    const { data } = await client.delete<ApiResponse<T>>(url);
    return unwrap(data);
  } catch (error) {
    throw toError(error);
  }
}
