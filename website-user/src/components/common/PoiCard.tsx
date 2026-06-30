import { motion } from 'framer-motion';
import { ArrowUpRight, MapPin, Star, Volume2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { getCopy } from '../../i18n/copy';
import { useAppStore } from '../../store/appStore';
import type { Poi } from '../../types/responses';
import { poiImage } from '../../utils/media';

import { useQuery } from '@tanstack/react-query';
import { categoryApi } from '../../api/categoryApi';

export function PoiCard({ poi, category }: { poi: Poi; category?: string }) {
  const lang = useAppStore((state) => state.lang);
  const ui = getCopy(lang);

  const { data: categories = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: categoryApi.list,
  });

  const matchedCategory = categories.find((c) => c.id === poi.categoryId);
  const displayCategory = matchedCategory
    ? (ui as any).categories?.[matchedCategory.code] || matchedCategory.name
    : category || ui.poiCard.defaultCategory;

  return (
    <motion.article whileHover={{ y: -6 }} className="group overflow-hidden rounded-3xl bg-white shadow-soft dark:bg-slate-900">
      <div className="relative h-48 overflow-hidden">
        <img className="h-full w-full object-cover transition duration-500 group-hover:scale-105" src={poiImage(poi)} alt={poi.name} />
        <span className="absolute left-4 top-4 rounded-full bg-white/90 px-3 py-1 text-xs font-bold text-coral backdrop-blur">
          {poi.priceRange || '$$'}
        </span>
      </div>

      <div className="p-5">
        <div className="mb-2 flex items-start justify-between gap-3">
          <p className="text-xs font-semibold uppercase tracking-wider text-teal">{displayCategory}</p>
          {poi.rating > 0 ? (
            <span className="flex items-center gap-1 text-sm font-semibold">
              <Star size={15} className="fill-amber-400 text-amber-400" />
              {poi.rating.toFixed(1)}
            </span>
          ) : null}
        </div>

        <h3 className="line-clamp-1 text-lg font-bold">{poi.name}</h3>
        <p className="mt-2 flex line-clamp-1 items-center gap-1.5 text-sm text-slate-500 dark:text-slate-400">
          <MapPin size={15} />
          {poi.address}
        </p>

        <div className="mt-4 flex items-center justify-between">
          <span className="flex items-center gap-1.5 text-xs text-slate-500">
            <Volume2 size={15} className="text-teal" />
            {ui.poiCard.audioGuide}
          </span>
          <Link className="flex items-center gap-1 text-sm font-bold text-coral" to={`/poi/${poi.id}`}>
            {ui.poiCard.explore} <ArrowUpRight size={16} />
          </Link>
        </div>
      </div>
    </motion.article>
  );
}
