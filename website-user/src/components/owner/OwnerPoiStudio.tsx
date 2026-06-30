import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { audioApi } from '../../api/audioApi';
import { ownerApi } from '../../api/ownerApi';
import { ErrorBox } from '../common/ErrorBox';
import { Field, TextArea, TextInput } from '../common/FormControls';
import { Spinner } from '../common/Spinner';
import type {
  CreatePoiLocalizationRequest,
  GeneratePoiAudioRequest,
  UploadPoiAudioRequest,
} from '../../types/requests';
import type { Lang, OwnerManagedPoi } from '../../types/responses';
import { normalizeMediaUrl } from '../../utils/media';

const SUPPORTED_LANGUAGES: Lang[] = ['vi', 'en', 'zh', 'ja', 'ko', 'fr', 'de', 'es', 'th', 'ru'];

function validateLocalizationDraft(draft: CreatePoiLocalizationRequest, lang: Lang): string | null {
  if (lang === 'vi') {
    return null;
  }

  if (!draft.name.trim()) {
    return 'Ten hien thi khong duoc de trong.';
  }

  if (!draft.description.trim()) {
    return 'Mo ta localization khong duoc de trong. Hay tu nhap noi dung hoac bam "Tu dong dich tu tieng Viet".';
  }

  return null;
}

function buildLocalizationDraft(
  poi: OwnerManagedPoi,
  lang: Lang,
  localization?: {
    name: string;
    description: string;
    ttsScript?: string | null;
    audioUrl?: string | null;
    isFallback: boolean;
  } | null,
): CreatePoiLocalizationRequest {
  if (lang === 'vi') {
    return {
      lang,
      name: poi.name,
      description: poi.description,
      ttsScript: poi.ttsScript ?? '',
      audioUrl: '',
      isFallback: false,
    };
  }

  if (localization) {
    return {
      lang,
      name: localization.name,
      description: localization.description,
      ttsScript: localization.ttsScript ?? '',
      audioUrl: localization.audioUrl ?? '',
      isFallback: localization.isFallback,
    };
  }

  return {
    lang,
    name: poi.name,
    description: '',
    ttsScript: '',
    audioUrl: '',
    isFallback: false,
  };
}

type OwnerPoiStudioProps = {
  pois: OwnerManagedPoi[];
  selectedPoiId?: string | null;
};

export function OwnerPoiStudio({ pois, selectedPoiId }: OwnerPoiStudioProps) {
  const queryClient = useQueryClient();
  const audioFileInputRef = useRef<HTMLInputElement>(null);
  const lastAutoTranslateKeyRef = useRef('');
  const [poiId, setPoiId] = useState('');
  const [lang, setLang] = useState<Lang>('vi');
  const [draft, setDraft] = useState<CreatePoiLocalizationRequest | null>(null);
  const [voiceName, setVoiceName] = useState('');
  const [manualAudioUrl, setManualAudioUrl] = useState('');
  const [manualAudioFile, setManualAudioFile] = useState<File | null>(null);
  const [notice, setNotice] = useState('');

  useEffect(() => {
    if (!poiId && pois.length > 0) {
      setPoiId(pois[0].id);
    }
  }, [poiId, pois]);

  useEffect(() => {
    if (selectedPoiId && pois.some((item) => item.id === selectedPoiId) && selectedPoiId !== poiId) {
      setPoiId(selectedPoiId);
    }
  }, [poiId, pois, selectedPoiId]);

  const selectedPoi = pois.find((item) => item.id === poiId) ?? null;

  const localizationsQuery = useQuery({
    queryKey: ['owner-poi-localizations', poiId],
    queryFn: () => ownerApi.poiLocalizations(poiId),
    enabled: Boolean(poiId),
  });

  const audioQuery = useQuery({
    queryKey: ['owner-poi-audio', poiId, lang],
    queryFn: () => audioApi.getPoiAudio(poiId, lang),
    enabled: Boolean(poiId),
  });

  const selectedLocalization =
    lang === 'vi'
      ? null
      : (localizationsQuery.data ?? []).find((item) => item.lang === lang) ?? null;

  useEffect(() => {
    if (!selectedPoi) {
      setDraft(null);
      return;
    }

    setDraft(buildLocalizationDraft(selectedPoi, lang, selectedLocalization));
  }, [lang, selectedLocalization, selectedPoi]);

  useEffect(() => {
    setVoiceName(audioQuery.data?.voiceName ?? '');
  }, [audioQuery.data?.voiceName, lang, poiId]);

  useEffect(() => {
    setManualAudioUrl('');
    setManualAudioFile(null);
    setNotice('');
    if (audioFileInputRef.current) {
      audioFileInputRef.current.value = '';
    }
  }, [lang, poiId]);

  const refreshWorkspace = async () => {
    if (!poiId) {
      return;
    }

    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['owner-poi-localizations', poiId] }),
      queryClient.invalidateQueries({ queryKey: ['owner-poi-audio', poiId, lang] }),
      queryClient.invalidateQueries({ queryKey: ['owner-pois'] }),
    ]);
  };

  const saveLocalizationMutation = useMutation({
    mutationFn: async () => {
      if (!poiId || !draft) {
        throw new Error('Chua chon POI.');
      }

      if (lang === 'vi') {
        throw new Error('Noi dung tieng Viet van di theo luong de xuat cap nhat.');
      }

      const validationMessage = validateLocalizationDraft(draft, lang);
      if (validationMessage) {
        throw new Error(validationMessage);
      }

      return ownerApi.savePoiLocalization(poiId, lang, {
        ...draft,
        lang,
        audioUrl: draft.audioUrl?.trim() || undefined,
        ttsScript: draft.ttsScript?.trim() || undefined,
      });
    },
    onSuccess: async () => {
      setNotice('Da luu ban dich cho POI nay.');
      await refreshWorkspace();
    },
  });

  const translateMutation = useMutation({
    mutationFn: async () => {
      if (!poiId) {
        throw new Error('Chua chon POI.');
      }

      if (lang === 'vi') {
        throw new Error('Khong can auto-translate cho tieng Viet.');
      }

      return ownerApi.translatePoiLocalization(poiId, {
        lang,
        sourceLang: 'vi',
        overwriteExisting: true,
      });
    },
    onSuccess: async () => {
      setNotice('Da dich noi dung va cap nhat localization.');
      await refreshWorkspace();
    },
  });

  useEffect(() => {
    if (!poiId || !selectedPoi || lang === 'vi') {
      return;
    }

    if (selectedLocalization || localizationsQuery.isLoading || localizationsQuery.isFetching || translateMutation.isPending) {
      return;
    }

    const autoTranslateKey = `${poiId}:${lang}`;
    if (lastAutoTranslateKeyRef.current === autoTranslateKey) {
      return;
    }

    lastAutoTranslateKeyRef.current = autoTranslateKey;
    setNotice('');
    translateMutation.mutate();
  }, [
    poiId,
    selectedPoi,
    lang,
    selectedLocalization,
    localizationsQuery.isLoading,
    localizationsQuery.isFetching,
    translateMutation.isPending,
  ]);

  const generateAudioMutation = useMutation({
    mutationFn: async () => {
      if (!poiId) {
        throw new Error('Chua chon POI.');
      }

      if (lang !== 'vi') {
        if (!draft) {
          throw new Error('Chua co noi dung localization de tao audio.');
        }

        const validationMessage = validateLocalizationDraft(draft, lang);
        if (validationMessage) {
          throw new Error(validationMessage);
        }

        await ownerApi.savePoiLocalization(poiId, lang, {
          ...draft,
          lang,
          audioUrl: draft.audioUrl?.trim() || undefined,
          ttsScript: draft.ttsScript?.trim() || undefined,
        });
      }

      const payload: GeneratePoiAudioRequest = {
        lang,
        voiceName: voiceName.trim() || undefined,
      };
      return ownerApi.generatePoiAudio(poiId, payload);
    },
    onSuccess: async () => {
      setNotice('Da tao audio cho ngon ngu dang chon.');
      await refreshWorkspace();
    },
  });

  const uploadAudioMutation = useMutation({
    mutationFn: async () => {
      if (!poiId) {
        throw new Error('Chua chon POI.');
      }

      const trimmedAudioUrl = manualAudioUrl.trim();
      if (!manualAudioFile && !trimmedAudioUrl) {
        throw new Error('Chon file audio hoac nhap URL audio truoc khi luu.');
      }

      const payload: UploadPoiAudioRequest = {
        lang,
        audioUrl: trimmedAudioUrl || undefined,
        voiceName: voiceName.trim() || undefined,
        sourceType: manualAudioFile ? 'uploaded' : 'manual_url',
      };
      return ownerApi.uploadPoiAudio(poiId, payload, manualAudioFile ?? undefined);
    },
    onSuccess: async () => {
      setManualAudioUrl('');
      setManualAudioFile(null);
      if (audioFileInputRef.current) {
        audioFileInputRef.current.value = '';
      }

      setNotice('Da cap nhat audio thu cong.');
      await refreshWorkspace();
    },
  });

  const deleteAudioMutation = useMutation({
    mutationFn: async () => {
      if (!poiId) {
        throw new Error('Chua chon POI.');
      }

      return ownerApi.deletePoiAudio(poiId, lang);
    },
    onSuccess: async () => {
      setManualAudioUrl('');
      setManualAudioFile(null);
      if (audioFileInputRef.current) {
        audioFileInputRef.current.value = '';
      }

      setNotice('Da xoa audio hien tai cho ngon ngu dang chon.');
      await refreshWorkspace();
    },
  });

  const busy =
    saveLocalizationMutation.isPending ||
    translateMutation.isPending ||
    generateAudioMutation.isPending ||
    uploadAudioMutation.isPending ||
    deleteAudioMutation.isPending;

  return (
    <div className="mt-8 rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold">Ban dich va audio cua chu quan</h2>
          <p className="mt-2 max-w-3xl text-sm text-slate-500">
            Chu quan co the auto-translate noi dung sang ngon ngu khac, chinh sua narration script, tao audio TTS,
            hoac upload audio override cho POI dang quan ly.
          </p>
        </div>
      </div>

      <div className="mt-6 grid gap-4 md:grid-cols-[1.5fr_.7fr]">
        <Field label="POI dang quan ly">
          <select
            value={poiId}
            onChange={(event) => {
              setNotice('');
              setPoiId(event.target.value);
            }}
            className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
          >
            {pois.map((poi) => (
              <option key={poi.id} value={poi.id}>
                {poi.name} - {poi.address}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Ngon ngu audio">
          <select
            value={lang}
            onChange={(event) => {
              setNotice('');
              setLang(event.target.value as Lang);
            }}
            className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
          >
            {SUPPORTED_LANGUAGES.map((value) => (
              <option key={value} value={value}>
                {value.toUpperCase()}
              </option>
            ))}
          </select>
        </Field>
      </div>

      {selectedPoi ? (
        <div className="mt-6 grid gap-6 xl:grid-cols-[1.15fr_.85fr]">
          <div className="rounded-3xl border border-slate-200 p-5 dark:border-slate-800">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-bold">{selectedPoi.name}</p>
                <p className="mt-1 text-sm text-slate-500">
                  {selectedPoi.address}, {selectedPoi.ward}, {selectedPoi.district}
                </p>
              </div>
              {lang !== 'vi' ? (
                <button
                  type="button"
                  className="btn-secondary !px-4 !py-2"
                  onClick={() => {
                    setNotice('');
                    translateMutation.mutate();
                  }}
                  disabled={busy}
                >
                  Tu dong dich tu tieng Viet
                </button>
              ) : null}
            </div>

            {lang === 'vi' ? (
              <div className="mt-4 rounded-2xl bg-slate-100 px-4 py-3 text-sm text-slate-600 dark:bg-slate-800">
                Noi dung tieng Viet hien dang lay tu POI goc. Neu muon doi ten, mo ta hoac script tieng Viet, hay gui
                de xuat cap nhat o khu ben duoi.
              </div>
            ) : !selectedLocalization && !localizationsQuery.isFetching ? (
              <div className="mt-4 rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-800">
                Chua co localization cho ngon ngu nay. Ban co the bam nut auto-translate hoac tu nhap noi dung.
              </div>
            ) : null}

            {localizationsQuery.isLoading && lang !== 'vi' ? (
              <div className="mt-4">
                <Spinner />
              </div>
            ) : localizationsQuery.isError ? (
              <div className="mt-4">
                <ErrorBox text={(localizationsQuery.error as Error).message} />
              </div>
            ) : draft ? (
              <div className="mt-5 grid gap-4">
                <Field label="Ten hien thi">
                  <TextInput
                    value={draft.name}
                    readOnly={lang === 'vi'}
                    onChange={(event) =>
                      setDraft((current) => (current ? { ...current, name: event.target.value } : current))
                    }
                  />
                </Field>
                <Field label="Mo ta">
                  <TextArea
                    value={draft.description}
                    readOnly={lang === 'vi'}
                    onChange={(event) =>
                      setDraft((current) => (current ? { ...current, description: event.target.value } : current))
                    }
                  />
                </Field>
                <Field label="Narration script">
                  <TextArea
                    value={draft.ttsScript ?? ''}
                    readOnly={lang === 'vi'}
                    onChange={(event) =>
                      setDraft((current) => (current ? { ...current, ttsScript: event.target.value } : current))
                    }
                    placeholder="Neu de trong, TTS se doc theo noi dung mo ta cua ngon ngu dang chon."
                  />
                </Field>
                {lang !== 'vi' ? (
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      className="btn-primary !px-4 !py-2"
                      onClick={() => {
                        setNotice('');
                        saveLocalizationMutation.mutate();
                      }}
                      disabled={busy}
                    >
                      Luu localization
                    </button>
                    <button
                      type="button"
                      className="btn-secondary !px-4 !py-2"
                      onClick={() => {
                        setNotice('');
                        generateAudioMutation.mutate();
                      }}
                      disabled={busy}
                    >
                      Luu va tao audio
                    </button>
                  </div>
                ) : (
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      className="btn-primary !px-4 !py-2"
                      onClick={() => {
                        setNotice('');
                        generateAudioMutation.mutate();
                      }}
                      disabled={busy}
                    >
                      Tao lai audio tieng Viet
                    </button>
                  </div>
                )}
              </div>
            ) : null}
          </div>

          <div className="rounded-3xl border border-slate-200 p-5 dark:border-slate-800">
            <h3 className="text-lg font-bold">Audio hien tai</h3>
            <p className="mt-2 text-sm text-slate-500">
              Ban co the chon voice hint cho TTS, tao audio tu script, hoac upload file audio override.
            </p>

            <div className="mt-4 grid gap-4">
              <Field label="Voice name">
                <TextInput
                  value={voiceName}
                  onChange={(event) => setVoiceName(event.target.value)}
                  placeholder="Vi du: en-US, ja-JP, zh-CN"
                />
              </Field>

              {audioQuery.isLoading ? (
                <Spinner />
              ) : audioQuery.isError ? (
                <ErrorBox text={(audioQuery.error as Error).message} />
              ) : audioQuery.data?.audioUrl ? (
                <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
                  <audio controls src={normalizeMediaUrl(audioQuery.data.audioUrl)} className="w-full" />
                  <p className="mt-3 text-xs text-slate-500">
                    Nguon: {audioQuery.data.sourceType || 'audio'} · Trang thai: {audioQuery.data.status || 'done'}
                  </p>
                </div>
              ) : (
                <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
                  Chua co audio cho ngon ngu nay.
                </div>
              )}

              <Field label="URL audio override">
                <TextInput
                  value={manualAudioUrl}
                  onChange={(event) => setManualAudioUrl(event.target.value)}
                  placeholder="Dan link MP3 neu muon override bang file co san"
                />
              </Field>

              <div className="grid gap-2">
                <input
                  ref={audioFileInputRef}
                  type="file"
                  accept="audio/*"
                  onChange={(event) => setManualAudioFile(event.target.files?.[0] ?? null)}
                />
                {manualAudioFile ? (
                  <p className="text-xs text-slate-500">File da chon: {manualAudioFile.name}</p>
                ) : null}
              </div>

              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  className="btn-secondary !px-4 !py-2"
                  onClick={() => {
                    setNotice('');
                    uploadAudioMutation.mutate();
                  }}
                  disabled={busy}
                >
                  Luu audio override
                </button>
                <button
                  type="button"
                  className="btn-secondary !border-rose-200 !text-rose-700 hover:!border-rose-300 hover:!text-rose-800 !px-4 !py-2"
                  onClick={() => {
                    if (!audioQuery.data?.audioUrl) {
                      setNotice('Chua co audio nao de xoa.');
                      return;
                    }

                    if (typeof window !== 'undefined' && !window.confirm('Xoa audio hien tai cua ngon ngu dang chon?')) {
                      return;
                    }

                    setNotice('');
                    deleteAudioMutation.mutate();
                  }}
                  disabled={busy || !audioQuery.data?.audioUrl}
                >
                  Xoa audio hien tai
                </button>
                <button
                  type="button"
                  className="btn-secondary !px-4 !py-2"
                  onClick={() => {
                    setManualAudioUrl('');
                    setManualAudioFile(null);
                    if (audioFileInputRef.current) {
                      audioFileInputRef.current.value = '';
                    }
                  }}
                  disabled={busy}
                >
                  Xoa du lieu tam
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {notice ? (
        <div className="mt-6 rounded-2xl bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          {notice}
        </div>
      ) : null}

      {saveLocalizationMutation.error ? (
        <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {(saveLocalizationMutation.error as Error).message}
        </div>
      ) : null}
      {translateMutation.error ? (
        <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {(translateMutation.error as Error).message}
        </div>
      ) : null}
      {generateAudioMutation.error ? (
        <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {(generateAudioMutation.error as Error).message}
        </div>
      ) : null}
      {uploadAudioMutation.error ? (
        <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {(uploadAudioMutation.error as Error).message}
        </div>
      ) : null}
      {deleteAudioMutation.error ? (
        <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {(deleteAudioMutation.error as Error).message}
        </div>
      ) : null}
    </div>
  );
}
