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
  async between(from: { lat: number; lng: number }, to: { lat: number; lng: number }): Promise<RouteSummary> {
    const coordinates = `${from.lng},${from.lat};${to.lng},${to.lat}`;
    const url = `${OSRM_BASE_URL}/route/v1/${encodeURIComponent(OSRM_PROFILE)}/${coordinates}?overview=full&geometries=geojson&steps=false`;
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error('Không lấy được dữ liệu chỉ đường trên bản đồ.');
    }

    const payload = (await response.json()) as OsrmRouteResponse;
    if (payload.code !== 'Ok' || !payload.routes?.[0]?.geometry?.coordinates?.length) {
      throw new Error('Không tìm thấy đường đi phù hợp cho hành trình này.');
    }

    return {
      distanceMeters: payload.routes[0].distance,
      durationSeconds: payload.routes[0].duration,
      geometry: payload.routes[0].geometry,
    };
  },
};

