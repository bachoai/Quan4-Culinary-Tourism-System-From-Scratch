import { Suspense, lazy } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { ProtectedRoute } from '../components/common/ProtectedRoute';
import { AdminLayout } from '../components/layout/AdminLayout';
import { useAuthStore } from '../store/authStore';

const AnalyticsPage = lazy(async () => ({ default: (await import('../pages/AnalyticsPage')).AnalyticsPage }));
const AudioPage = lazy(async () => ({ default: (await import('../pages/AudioPage')).AudioPage }));
const CategoryPage = lazy(async () => ({ default: (await import('../pages/CategoryPage')).CategoryPage }));
const DashboardPage = lazy(async () => ({ default: (await import('../pages/DashboardPage')).DashboardPage }));
const LocalizationPage = lazy(async () => ({ default: (await import('../pages/LocalizationPage')).LocalizationPage }));
const LoginPage = lazy(async () => ({ default: (await import('../pages/LoginPage')).LoginPage }));
const MapsPage = lazy(async () => ({ default: (await import('../pages/MapsPage')).MapsPage }));
const NotFoundPage = lazy(async () => ({ default: (await import('../pages/NotFoundPage')).NotFoundPage }));
const OwnerRegistrationPage = lazy(async () => ({ default: (await import('../pages/OwnerRegistrationPage')).OwnerRegistrationPage }));
const OwnerSubmissionPage = lazy(async () => ({ default: (await import('../pages/OwnerSubmissionPage')).OwnerSubmissionPage }));
const PoiCreatePage = lazy(async () => ({ default: (await import('../pages/PoiCreatePage')).PoiCreatePage }));
const PoiDetailPage = lazy(async () => ({ default: (await import('../pages/PoiDetailPage')).PoiDetailPage }));
const PoiEditPage = lazy(async () => ({ default: (await import('../pages/PoiEditPage')).PoiEditPage }));
const PoiListPage = lazy(async () => ({ default: (await import('../pages/PoiListPage')).PoiListPage }));
const TourManagementPage = lazy(async () => ({ default: (await import('../pages/TourManagementPage')).TourManagementPage }));
const UsageHistoryPage = lazy(async () => ({ default: (await import('../pages/UsageHistoryPage')).UsageHistoryPage }));
const UserManagementPage = lazy(async () => ({ default: (await import('../pages/UserManagementPage')).UserManagementPage }));

function IndexRedirect() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  return <Navigate to={isAuthenticated ? '/admin/dashboard' : '/login'} replace />;
}

export function AppRoutes() {
  return (
    <Suspense fallback={<LoadingScreen />}>
      <Routes>
        <Route path="/" element={<IndexRedirect />} />
        <Route path="/login" element={<LoginPage />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<AdminLayout />}>
            <Route path="/admin/dashboard" element={<DashboardPage />} />
            <Route path="/admin/categories" element={<CategoryPage />} />
            <Route path="/admin/pois" element={<PoiListPage />} />
            <Route path="/admin/pois/create" element={<PoiCreatePage />} />
            <Route path="/admin/pois/:id" element={<PoiDetailPage />} />
            <Route path="/admin/pois/:id/edit" element={<PoiEditPage />} />
            <Route path="/admin/owner-registrations" element={<OwnerRegistrationPage />} />
            <Route path="/admin/submissions" element={<OwnerSubmissionPage />} />
            <Route path="/admin/users" element={<UserManagementPage />} />
            <Route path="/admin/audio" element={<AudioPage />} />
            <Route path="/admin/localizations" element={<LocalizationPage />} />
            <Route path="/admin/analytics" element={<AnalyticsPage />} />
            <Route path="/admin/usage-history" element={<UsageHistoryPage />} />
            <Route path="/admin/tours" element={<TourManagementPage />} />
            <Route path="/admin/maps" element={<MapsPage />} />
          </Route>
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  );
}
