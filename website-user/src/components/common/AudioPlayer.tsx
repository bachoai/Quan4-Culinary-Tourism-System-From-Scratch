import { Pause, Play, Volume2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { getCopy, type UiLanguage } from '../../i18n/copy';
import type { Lang } from '../../types/responses';
import { normalizeMediaUrl } from '../../utils/media';

type AudioPlayerProps = {
  audioUrl?: string;
  text?: string;
  uiLang: UiLanguage;
  narrationLang: Lang;
  onPlay: (mode: 'audio' | 'tts') => void;
  autoplay?: boolean;
  loading?: boolean;
  errorText?: string;
  languageOptions?: Array<{ value: Lang; label: string }>;
  onLanguageChange?: (lang: Lang) => void;
};

const narrationLangMap: Record<string, string> = {
  vi: 'vi-VN',
  en: 'en-US',
  zh: 'zh-CN',
  ja: 'ja-JP',
  ko: 'ko-KR',
};

function resolveVoice(lang: string, voices: SpeechSynthesisVoice[]) {
  const preferredLang = (narrationLangMap[lang] ?? lang).toLowerCase();
  const baseLang = preferredLang.split('-')[0];

  return (
    voices.find((voice) => voice.lang.toLowerCase() === preferredLang) ??
    voices.find((voice) => voice.lang.toLowerCase().startsWith(`${baseLang}-`)) ??
    voices.find((voice) => voice.lang.toLowerCase() === baseLang) ??
    null
  );
}

export function AudioPlayer({
  audioUrl,
  text,
  uiLang,
  narrationLang,
  onPlay,
  autoplay = false,
  loading = false,
  errorText,
  languageOptions,
  onLanguageChange,
}: AudioPlayerProps) {
  const ui = getCopy(uiLang);
  const audio = useRef<HTMLAudioElement>(null);
  const autoStarted = useRef(false);
  const utterance = useRef<SpeechSynthesisUtterance | null>(null);
  const [playing, setPlaying] = useState(false);
  const [ttsVoice, setTtsVoice] = useState<SpeechSynthesisVoice | null>(null);
  const [voiceChecked, setVoiceChecked] = useState(false);
  const [playbackError, setPlaybackError] = useState<string>('');
  const narrationText = text?.trim() || '';
  const hasSpeechSynthesis = typeof window !== 'undefined' && 'speechSynthesis' in window;
  const hasNarration = Boolean(narrationText);
  const hasAudio = Boolean(audioUrl);
  const hasMatchingVoice = Boolean(ttsVoice);
  const canUseTts = hasNarration && hasSpeechSynthesis && hasMatchingVoice;
  const showLanguageSelector = Boolean(languageOptions?.length && onLanguageChange);

  useEffect(() => {
    if (!hasSpeechSynthesis) {
      setTtsVoice(null);
      setVoiceChecked(true);
      return;
    }

    const syncVoice = () => {
      const nextVoice = resolveVoice(narrationLang, window.speechSynthesis.getVoices());
      setTtsVoice(nextVoice);
      setVoiceChecked(true);
    };

    syncVoice();
    window.speechSynthesis.addEventListener('voiceschanged', syncVoice);

    return () => {
      window.speechSynthesis.removeEventListener('voiceschanged', syncVoice);
    };
  }, [hasSpeechSynthesis, narrationLang]);

  useEffect(() => {
    autoStarted.current = false;
    if (hasSpeechSynthesis) {
      window.speechSynthesis.cancel();
    }

    if (audio.current) {
      audio.current.pause();
      audio.current.currentTime = 0;
    }

    setPlaying(false);
    setPlaybackError('');
  }, [audioUrl, narrationText, hasSpeechSynthesis, narrationLang]);

  useEffect(() => {
    return () => {
      if (hasSpeechSynthesis) {
        window.speechSynthesis.cancel();
      }
    };
  }, [hasSpeechSynthesis]);

  useEffect(() => {
    if (!autoplay || autoStarted.current) {
      return;
    }

    if (hasAudio) {
      autoStarted.current = true;
      void startNarration();
      return;
    }

    if (loading) {
      return;
    }

    if (!voiceChecked && hasNarration && hasSpeechSynthesis) {
      return;
    }

    if (!canUseTts) {
      return;
    }

    autoStarted.current = true;
    void startNarration();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoplay, canUseTts, hasAudio, hasNarration, hasSpeechSynthesis, loading, voiceChecked]);

  if (!hasAudio && !hasNarration) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        {ui.audio.noNarration}
      </div>
    );
  }

  if (!hasAudio && !hasSpeechSynthesis) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        {ui.audio.browserUnsupported}
      </div>
    );
  }

  if (!hasAudio && loading) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        {ui.audio.loadingAudio}
      </div>
    );
  }

  if (!hasAudio && voiceChecked && !hasMatchingVoice) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        {ui.audio.noVoice}
      </div>
    );
  }

  const stopNarration = () => {
    if (audio.current) {
      audio.current.pause();
      audio.current.currentTime = 0;
    }

    if (hasSpeechSynthesis) {
      window.speechSynthesis.cancel();
    }

    setPlaying(false);
  };

  const startAudio = async () => {
    if (!audio.current || !hasAudio) {
      return;
    }

    setPlaybackError('');

    try {
      await audio.current.play();
      setPlaying(true);
      onPlay('audio');
    } catch {
      setPlaying(false);
      setPlaybackError(ui.audio.cannotPlayFile);
    }
  };

  const startTts = async () => {
    if (!canUseTts || !hasSpeechSynthesis) {
      return;
    }

    window.speechSynthesis.cancel();
    setPlaybackError('');

    const nextUtterance = new SpeechSynthesisUtterance(narrationText);
    nextUtterance.lang = narrationLangMap[narrationLang] ?? narrationLang;
    if (ttsVoice) {
      nextUtterance.voice = ttsVoice;
    }
    nextUtterance.onstart = () => {
      setPlaying(true);
      onPlay('tts');
    };
    nextUtterance.onend = () => {
      setPlaying(false);
    };
    nextUtterance.onerror = () => {
      setPlaying(false);
      setPlaybackError(ui.audio.cannotReadNow);
    };

    utterance.current = nextUtterance;
    window.speechSynthesis.speak(nextUtterance);
  };

  async function startNarration() {
    if (hasAudio) {
      await startAudio();
      return;
    }

    await startTts();
  }

  const toggle = async () => {
    if (playing) {
      stopNarration();
      return;
    }

    await startNarration();
  };

  return (
    <div className="rounded-2xl bg-teal/10 p-4">
      <div className="flex flex-wrap items-center gap-4">
        <button
          onClick={toggle}
          className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-teal text-white"
        >
          {playing ? <Pause size={19} /> : <Play size={19} />}
        </button>

        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-2 text-sm font-bold">
            <Volume2 size={16} />
            {hasAudio ? ui.audio.audioGuideTitle : ui.audio.ttsGuideTitle}
          </p>
          <p className="text-xs text-slate-500">
            {hasAudio
              ? ui.audio.audioGuideText
              : voiceChecked
                ? ui.audio.ttsGuideText
                : ui.audio.checkingVoice}
          </p>
          {errorText || playbackError ? (
            <p className="mt-1 text-xs text-rose-600">{errorText || playbackError}</p>
          ) : null}
        </div>

        {showLanguageSelector ? (
          <label className="flex min-w-[11rem] flex-col gap-1 text-xs font-semibold text-slate-600 dark:text-slate-300">
            <span>{ui.common.languageLabel}</span>
            <select
              value={narrationLang}
              onChange={(event) => onLanguageChange?.(event.target.value as Lang)}
              className="rounded-2xl border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
            >
              {languageOptions?.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        ) : null}
      </div>

      <audio
        ref={audio}
        src={normalizeMediaUrl(audioUrl)}
        preload="metadata"
        onPause={() => setPlaying(false)}
        onEnded={() => {
          setPlaying(false);
        }}
        onError={() => {
          setPlaying(false);
          setPlaybackError(ui.audio.fileUnavailable);
        }}
      />
    </div>
  );
}
