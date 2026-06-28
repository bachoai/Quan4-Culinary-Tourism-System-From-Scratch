import { useEffect, useRef, useState } from 'react';
import maplibregl from 'maplibre-gl';
import type { RouteGeometry } from '../../api/routeApi';
import type { Poi } from '../../types/responses';

const mapTilerKey = import.meta.env.VITE_MAPTILER_KEY as string | undefined;
const mapStyle: string | maplibregl.StyleSpecification = mapTilerKey
  ? `https://api.maptiler.com/maps/streets-v2/style.json?key=${mapTilerKey}`
  : {
      version: 8,
      sources: {
        osm: {
          type: 'raster',
          tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
          tileSize: 256,
          attribution: 'Â© OpenStreetMap contributors',
        },
      },
      layers: [{ id: 'osm', type: 'raster', source: 'osm' }],
    };

const ROUTE_SOURCE_ID = 'user-route';
const ROUTE_LAYER_ID = 'user-route-line';

function popupContent(poi: Poi, onSelectPoi?: (poiId: string) => void) {
  const root = document.createElement('div');
  root.className = 'space-y-2';

  const title = document.createElement('strong');
  title.textContent = poi.name;

  const address = document.createElement('div');
  address.className = 'text-xs text-slate-500';
  address.textContent = poi.address;

  const actions = document.createElement('div');
  actions.className = 'mt-2 flex gap-2';

  if (onSelectPoi) {
    const selectButton = document.createElement('button');
    selectButton.type = 'button';
    selectButton.textContent = 'Chỉ đường';
    selectButton.className = 'rounded-full bg-teal px-3 py-1 text-xs font-bold text-white';
    selectButton.onclick = () => onSelectPoi(poi.id);
    actions.append(selectButton);
  }

  const detailLink = document.createElement('a');
  detailLink.href = `${window.location.origin}${window.location.pathname}#/poi/${encodeURIComponent(poi.id)}`;
  detailLink.textContent = 'Xem chi tiết';
  detailLink.className = 'rounded-full border border-slate-300 px-3 py-1 text-xs font-bold text-slate-700';
  actions.append(detailLink);

  root.append(title, address, actions);
  return root;
}

function fitToCoordinates(map: maplibregl.Map, coordinates: [number, number][]) {
  if (!coordinates.length) {
    return;
  }

  const bounds = coordinates.reduce(
    (current, coordinate) => current.extend(coordinate),
    new maplibregl.LngLatBounds(coordinates[0], coordinates[0]),
  );

  map.fitBounds(bounds, { padding: 56, maxZoom: 15, duration: 700 });
}

export function PoiMap({
  pois,
  userLocation,
  selectedPoiId,
  routeGeometry,
  onSelectPoi,
}: {
  pois: Poi[];
  userLocation?: { lat: number; lng: number };
  selectedPoiId?: string;
  routeGeometry?: RouteGeometry | null;
  onSelectPoi?: (poiId: string) => void;
}) {
  const node = useRef<HTMLDivElement>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const poiMarkersRef = useRef<maplibregl.Marker[]>([]);
  const userMarkerRef = useRef<maplibregl.Marker | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!node.current || mapRef.current) {
      return;
    }

    try {
      const map = new maplibregl.Map({
        container: node.current,
        style: mapStyle,
        center: [106.706, 10.7578],
        zoom: 14,
      });

      map.addControl(new maplibregl.NavigationControl({ showCompass: true }), 'top-right');
      map.on('error', () => setFailed(true));
      mapRef.current = map;
    } catch {
      setFailed(true);
    }

    return () => {
      poiMarkersRef.current.forEach((marker) => marker.remove());
      poiMarkersRef.current = [];
      userMarkerRef.current?.remove();
      userMarkerRef.current = null;
      mapRef.current?.remove();
      mapRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) {
      return;
    }

    poiMarkersRef.current.forEach((marker) => marker.remove());
    poiMarkersRef.current = [];

    const validPois = pois.filter((poi) => Number.isFinite(poi.latitude) && Number.isFinite(poi.longitude));
    validPois.forEach((poi) => {
      const marker = new maplibregl.Marker({ color: poi.id === selectedPoiId ? '#0f172a' : '#FF6B35' })
        .setLngLat([poi.longitude, poi.latitude])
        .setPopup(new maplibregl.Popup({ offset: 22 }).setDOMContent(popupContent(poi, onSelectPoi)))
        .addTo(map);

      marker.getElement().addEventListener('click', () => onSelectPoi?.(poi.id));
      poiMarkersRef.current.push(marker);
    });

    if (!routeGeometry?.coordinates.length) {
      const focusCoordinates: [number, number][] = validPois.map((poi) => [poi.longitude, poi.latitude]);
      if (userLocation) {
        focusCoordinates.push([userLocation.lng, userLocation.lat]);
      }

      if (focusCoordinates.length > 1) {
        fitToCoordinates(map, focusCoordinates);
      } else if (focusCoordinates[0]) {
        map.easeTo({ center: focusCoordinates[0], zoom: 15, duration: 700 });
      }
    }
  }, [onSelectPoi, pois, routeGeometry, selectedPoiId, userLocation]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) {
      return;
    }

    userMarkerRef.current?.remove();
    userMarkerRef.current = null;

    if (!userLocation) {
      return;
    }

    userMarkerRef.current = new maplibregl.Marker({ color: '#2EC4B6' })
      .setLngLat([userLocation.lng, userLocation.lat])
      .setPopup(new maplibregl.Popup().setText('Vị trí của bạn'))
      .addTo(map);
  }, [userLocation]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) {
      return;
    }

    const syncRoute = () => {
      const existingLayer = map.getLayer(ROUTE_LAYER_ID);
      const existingSource = map.getSource(ROUTE_SOURCE_ID);

      if (!routeGeometry?.coordinates.length) {
        if (existingLayer) {
          map.removeLayer(ROUTE_LAYER_ID);
        }
        if (existingSource) {
          map.removeSource(ROUTE_SOURCE_ID);
        }
        return;
      }

      const data = {
        type: 'Feature' as const,
        properties: {},
        geometry: routeGeometry,
      };

      if (existingSource) {
        (existingSource as maplibregl.GeoJSONSource).setData(data);
      } else {
        map.addSource(ROUTE_SOURCE_ID, {
          type: 'geojson',
          data,
        });
      }

      if (!map.getLayer(ROUTE_LAYER_ID)) {
        map.addLayer({
          id: ROUTE_LAYER_ID,
          type: 'line',
          source: ROUTE_SOURCE_ID,
          layout: {
            'line-cap': 'round',
            'line-join': 'round',
          },
          paint: {
            'line-color': '#14b8a6',
            'line-width': 6,
            'line-opacity': 0.9,
          },
        });
      }

      fitToCoordinates(map, routeGeometry.coordinates);
    };

    if (map.isStyleLoaded()) {
      syncRoute();
      return;
    }

    map.once('load', syncRoute);
    return () => {
      map.off('load', syncRoute);
    };
  }, [routeGeometry]);

  if (failed) {
    return (
      <div className="grid min-h-[620px] place-items-center rounded-[2rem] bg-slate-100 p-8 text-center text-slate-500 md:min-h-[700px] xl:min-h-[760px] dark:bg-slate-900">
        Không thể tải nền bản đồ. Bạn vẫn có thể xem danh sách POI và chỉ đường bên cạnh.
      </div>
    );
  }

  return (
    <div
      ref={node}
      className="min-h-[620px] overflow-hidden rounded-[2rem] bg-slate-200 md:min-h-[700px] xl:min-h-[760px] dark:bg-slate-800"
      aria-label="Bản đồ POI Quận 4"
    />
  );
}

