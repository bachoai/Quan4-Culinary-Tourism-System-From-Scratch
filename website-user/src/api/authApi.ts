import { getData, postData } from './axiosClient';
import type {
  AuthResponse,
  CurrentUser,
  OwnerRegistrationResponse,
} from '../types/responses';
import type {
  CreateOwnerRegistrationRequest,
  LoginRequest,
  RegisterRequest,
} from '../types/requests';

export const authApi = {
  login: (payload: LoginRequest) => postData<AuthResponse>('/api/v1/auth/login', payload),
  register: (payload: RegisterRequest) => postData<AuthResponse>('/api/v1/auth/register', payload),
  me: () => getData<CurrentUser>('/api/v1/auth/me'),
  registerOwner: (payload: CreateOwnerRegistrationRequest) =>
    postData<OwnerRegistrationResponse>('/api/v1/auth/register-owner', payload),
};
