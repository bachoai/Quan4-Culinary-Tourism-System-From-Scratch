export type RouteGeometry = {
  type: 'LineString';
  coordinates: [number, number][];
};

export type RouteSummary = {
  distanceMeters: number;
  durationSeconds: number;
  geometry: RouteGeometry;
};

const OSRM_BASE_URL = (import.meta.env.VITE_OSRM_BASE_URL || 'https://router.project-osrm.org').replace(/\/$/, '');
const OSRM_PROFILE = import.meta.env.VITE_OSRM_PROFILE || 'driving';

type OsrmRouteResponse = {
  code: string;
  routes?: Array<{
    distance: number;
    duration: number;
    geometry: RouteGeometry;
  }>;
};

export const routeApi = {
  async through(points: Array<{ lat: number; lng: number }>): Promise<RouteSummary> {
    if (points.length < 2) {
      throw new Error('Can it nhat hai diem de ve lo trinh.');
    }

    const coordinates = points.map((point) => `${point.lng},${point.lat}`).join(';');
    const url = `${OSRM_BASE_URL}/route/v1/${encodeURIComponent(OSRM_PROFILE)}/${coordinates}?overview=full&geometries=geojson&steps=false`;
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error('Khong lay duoc du lieu chi duong tren ban do.');
    }

    const payload = (await response.json()) as OsrmRouteResponse;
    if (payload.code !== 'Ok' || !payload.routes?.[0]?.geometry?.coordinates?.length) {
      throw new Error('Khong tim thay duong di phu hop cho hanh trinh nay.');
    }

    return {
      distanceMeters: payload.routes[0].distance,
      durationSeconds: payload.routes[0].duration,
      geometry: payload.routes[0].geometry,
    };
  },
  async between(from: { lat: number; lng: number }, to: { lat: number; lng: number }): Promise<RouteSummary> {
    return this.through([from, to]);
  },
};

