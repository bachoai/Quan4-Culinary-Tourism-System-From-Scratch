import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Search, Utensils } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { poiApi } from '../api/poiApi';
import { Categories } from '../components/common/Categories';
import { ErrorBox } from '../components/common/ErrorBox';
import { PoiCard } from '../components/common/PoiCard';
import { Spinner } from '../components/common/Spinner';
import { getCopy } from '../i18n/copy';
import { useAppStore } from '../store/appStore';
import { track } from '../utils/analytics';

const PAGE_SIZE = 9;

function buildSearch(keyword: string, categoryId: string, priceRange: string, page = 1) {
  const next = new URLSearchParams();
  if (keyword) {
    next.set('q', keyword);
  }
  if (categoryId) {
    next.set('category', categoryId);
  }
  if (priceRange) {
    next.set('price', priceRange);
  }
  if (page > 1) {
    next.set('page', String(page));
  }
  return next.toString();
}

function parsePage(value: string | null) {
  const parsed = Number.parseInt(value || '1', 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
}

function buildPageList(currentPage: number, totalPages: number) {
  const pages = new Set([1, totalPages, currentPage - 1, currentPage, currentPage + 1]);
  const ordered = [...pages].filter((page) => page >= 1 && page <= totalPages).sort((left, right) => left - right);
  const result: Array<number | 'ellipsis'> = [];

  ordered.forEach((page, index) => {
    const previous = ordered[index - 1];
    if (typeof previous === 'number' && page - previous > 1) {
      result.push('ellipsis');
    }

    result.push(page);
  });

  return result;
}

export default function ExplorePage() {
  const { lang } = useAppStore();
  const navigate = useNavigate();
  const location = useLocation();
  const ui = getCopy(lang);
  const params = new URLSearchParams(location.search);
  const appliedKeyword = params.get('q') || '';
  const appliedCategoryId = params.get('category') || '';
  const appliedPriceRange = params.get('price') || '';
  const requestedPage = parsePage(params.get('page'));
  const [keyword, setKeyword] = useState(appliedKeyword);
  const [categoryId, setCategoryId] = useState(appliedCategoryId);
  const [priceRange, setPriceRange] = useState(appliedPriceRange);

  useEffect(() => {
    const nextParams = new URLSearchParams(location.search);
    setKeyword(nextParams.get('q') || '');
    setCategoryId(nextParams.get('category') || '');
    setPriceRange(nextParams.get('price') || '');
  }, [location.search]);

  const query = useQuery({
    queryKey: ['explore', lang, appliedKeyword, appliedCategoryId, appliedPriceRange],
    queryFn: () => {
      return poiApi.list({
        lang,
        keyword: appliedKeyword || undefined,
        categoryId: appliedCategoryId || undefined,
        priceRange: appliedPriceRange || undefined,
      });
    },
  });

  const totalItems = query.data?.length ?? 0;
  const totalPages = totalItems > 0 ? Math.ceil(totalItems / PAGE_SIZE) : 1;
  const currentPage = Math.min(requestedPage, totalPages);
  const pageItems = buildPageList(currentPage, totalPages);
  const pagedPois = query.data?.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE) ?? [];
  const visibleFrom = totalItems > 0 ? (currentPage - 1) * PAGE_SIZE + 1 : 0;
  const visibleTo = totalItems > 0 ? Math.min(currentPage * PAGE_SIZE, totalItems) : 0;

  const pushFilters = (nextKeyword: string, nextCategoryId: string, nextPriceRange: string, nextPage = 1) => {
    const search = buildSearch(nextKeyword, nextCategoryId, nextPriceRange, nextPage);
    navigate(`/explore${search ? `?${search}` : ''}`);
  };

  const runSearch = (event: React.FormEvent) => {
    event.preventDefault();
    try {
      track('search_executed', lang, undefined, {
        hasKeyword: Boolean(keyword),
        categoryId: categoryId || undefined,
        priceRange: priceRange || undefined,
      });
    } catch (error) {
      console.warn('Search analytics unavailable', error);
    }
    pushFilters(keyword, categoryId, priceRange);
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">{ui.explore.kicker}</p>
      <h1 className="mt-2 text-4xl font-bold">{ui.explore.title}</h1>
      <p className="mt-3 max-w-2xl text-slate-500">
        {ui.explore.subtitle}
      </p>

      <form
        onSubmit={runSearch}
        className="mt-7 flex rounded-2xl border border-slate-200 bg-white p-2 dark:border-slate-800 dark:bg-slate-900"
      >
        <Search className="m-2 text-slate-400" />
        <input
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
          className="min-w-0 flex-1 bg-transparent outline-none"
          placeholder={ui.explore.searchPlaceholder}
        />
        <button className="btn-primary shrink-0">
          {ui.common.search}
        </button>
      </form>

      <div className="mt-6">
        <Categories
          selected={categoryId}
          onSelect={(id) => pushFilters(keyword, id || '', priceRange)}
        />
      </div>

      <div className="mt-4 flex gap-2">
        <span className="self-center text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">
          {ui.explore.priceLabel}
        </span>
        {['$', '$$', '$$$'].map((price) => (
          <button
            type="button"
            key={price}
            onClick={() => pushFilters(keyword, categoryId, priceRange === price ? '' : price)}
            className={`pill ${priceRange === price ? 'border-coral bg-orange-50 text-coral' : ''}`}
          >
            {price}
          </button>
        ))}
      </div>

      {query.isLoading ? (
        <Spinner />
      ) : query.isError ? (
        <ErrorBox text={(query.error as Error).message} />
      ) : totalItems ? (
        <>
          <div className="mt-6 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500">
            <span>
              {lang === 'en'
                ? `Showing ${visibleFrom}-${visibleTo} of ${totalItems} places`
                : `Hiển thị ${visibleFrom}-${visibleTo} trên ${totalItems} địa điểm`}
            </span>
            <span>{lang === 'en' ? `Page ${currentPage}/${totalPages}` : `Trang ${currentPage}/${totalPages}`}</span>
          </div>

          <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {pagedPois.map((poi) => (
              <PoiCard key={poi.id} poi={poi} />
            ))}
          </div>

          {totalPages > 1 ? (
            <nav
              className="mt-8 flex flex-wrap items-center justify-center gap-2"
              aria-label={lang === 'en' ? 'Explore pagination' : 'Phân trang khám phá'}
            >
              <button
                type="button"
                onClick={() => pushFilters(appliedKeyword, appliedCategoryId, appliedPriceRange, currentPage - 1)}
                disabled={currentPage === 1}
                className="inline-flex items-center gap-2 rounded-full border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:border-coral hover:text-coral disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200"
              >
                <ChevronLeft size={16} />
                {lang === 'en' ? 'Previous' : 'Trước'}
              </button>

              {pageItems.map((item, index) =>
                item === 'ellipsis' ? (
                  <span key={`ellipsis-${index}`} className="px-2 text-slate-400">
                    ...
                  </span>
                ) : (
                  <button
                    key={item}
                    type="button"
                    onClick={() => pushFilters(appliedKeyword, appliedCategoryId, appliedPriceRange, item)}
                    aria-current={item === currentPage ? 'page' : undefined}
                    className={`h-11 min-w-11 rounded-full border px-4 text-sm font-semibold transition ${
                      item === currentPage
                        ? 'border-coral bg-coral text-white'
                        : 'border-slate-200 text-slate-700 hover:border-coral hover:text-coral dark:border-slate-700 dark:text-slate-200'
                    }`}
                  >
                    {item}
                  </button>
                ),
              )}

              <button
                type="button"
                onClick={() => pushFilters(appliedKeyword, appliedCategoryId, appliedPriceRange, currentPage + 1)}
                disabled={currentPage === totalPages}
                className="inline-flex items-center gap-2 rounded-full border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:border-coral hover:text-coral disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200"
              >
                {lang === 'en' ? 'Next' : 'Sau'}
                <ChevronRight size={16} />
              </button>
            </nav>
          ) : null}
        </>
      ) : (
        <div className="mt-8 rounded-3xl bg-slate-100 p-12 text-center dark:bg-slate-900">
          <Utensils className="mx-auto text-teal" />
          <h2 className="mt-3 text-xl font-bold">{ui.explore.emptyTitle}</h2>
          <p className="mt-1 text-slate-500">{ui.explore.emptyDescription}</p>
        </div>
      )}
    </section>
  );
}
