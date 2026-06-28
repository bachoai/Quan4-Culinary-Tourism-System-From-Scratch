import { Pause, Play, Volume2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { normalizeMediaUrl } from '../../utils/media';

type AudioPlayerProps = {
  audioUrl?: string;
  text?: string;
  lang: string;
  onPlay: (mode: 'audio' | 'tts') => void;
  autoplay?: boolean;
};

const narrationLangMap: Record<string, string> = {
  vi: 'vi-VN',
  en: 'en-US',
  zh: 'zh-CN',
  ja: 'ja-JP',
  ko: 'ko-KR',
};

function resolveVoice(lang: string) {
  const preferredLang = narrationLangMap[lang] ?? lang;
  const voices = window.speechSynthesis.getVoices();

  return (
    voices.find((voice) => voice.lang.toLowerCase() === preferredLang.toLowerCase()) ??
    voices.find((voice) => voice.lang.toLowerCase().startsWith(lang.toLowerCase())) ??
    null
  );
}

export function AudioPlayer({ audioUrl, text, lang, onPlay, autoplay = false }: AudioPlayerProps) {
  const audio = useRef<HTMLAudioElement>(null);
  const autoStarted = useRef(false);
  const utterance = useRef<SpeechSynthesisUtterance | null>(null);
  const [playing, setPlaying] = useState(false);
  const [mode, setMode] = useState<'audio' | 'tts' | null>(null);
  const narrationText = text?.trim() || '';
  const hasTts = Boolean(narrationText) && typeof window !== 'undefined' && 'speechSynthesis' in window;
  const hasAudio = Boolean(audioUrl);

  useEffect(() => {
    autoStarted.current = false;
    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      window.speechSynthesis.cancel();
    }

    if (audio.current) {
      audio.current.pause();
      audio.current.currentTime = 0;
    }

    setPlaying(false);
    setMode(null);
  }, [audioUrl, narrationText, lang]);

  useEffect(() => {
    return () => {
      if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
        window.speechSynthesis.cancel();
      }
    };
  }, []);

  useEffect(() => {
    if (!autoplay || autoStarted.current || (!hasTts && !hasAudio)) {
      return;
    }

    autoStarted.current = true;
    void startNarration();
  }, [autoplay, hasAudio, hasTts, lang, narrationText]);

  if (!hasTts && !hasAudio) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        Dia diem nay chua co noi dung thuyet minh.
      </div>
    );
  }

  const stopNarration = () => {
    if (audio.current) {
      audio.current.pause();
      audio.current.currentTime = 0;
    }

    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      window.speechSynthesis.cancel();
    }

    setPlaying(false);
    setMode(null);
  };

  const startAudio = async () => {
    if (!audio.current || !hasAudio) {
      return;
    }

    await audio.current.play();
    setMode('audio');
    setPlaying(true);
    onPlay('audio');
  };

  const startTts = async () => {
    if (!hasTts || typeof window === 'undefined' || !('speechSynthesis' in window)) {
      await startAudio();
      return;
    }

    window.speechSynthesis.cancel();

    const nextUtterance = new SpeechSynthesisUtterance(narrationText);
    nextUtterance.lang = narrationLangMap[lang] ?? lang;
    nextUtterance.voice = resolveVoice(lang);
    nextUtterance.onstart = () => {
      setMode('tts');
      setPlaying(true);
      onPlay('tts');
    };
    nextUtterance.onend = () => {
      setPlaying(false);
      setMode(null);
    };
    nextUtterance.onerror = () => {
      setPlaying(false);
      setMode(null);
      if (hasAudio) {
        void startAudio();
      }
    };

    utterance.current = nextUtterance;
    window.speechSynthesis.speak(nextUtterance);
  };

  async function startNarration() {
    if (hasTts) {
      await startTts();
      return;
    }

    await startAudio();
  }

  const toggle = async () => {
    if (playing) {
      stopNarration();
      return;
    }

    await startNarration();
  };

  return (
    <div className="flex items-center gap-4 rounded-2xl bg-teal/10 p-4">
      <button
        onClick={toggle}
        className="grid h-11 w-11 place-items-center rounded-full bg-teal text-white"
      >
        {playing ? <Pause size={19} /> : <Play size={19} />}
      </button>

      <div className="min-w-0 flex-1">
        <p className="flex items-center gap-2 text-sm font-bold">
          <Volume2 size={16} />
          {mode === 'tts' || hasTts ? 'Thuyet minh bang giong doc' : 'Audio thuyet minh'}
        </p>
        <p className="text-xs text-slate-500">
          {hasTts
            ? 'Doc tu kich ban thuyet minh hoac phan mo ta da chinh sua.'
            : 'Lang nghe cau chuyen cua dia diem nay.'}
        </p>
      </div>

      <audio
        ref={audio}
        src={normalizeMediaUrl(audioUrl)}
        onPause={() => setPlaying(false)}
        onEnded={() => {
          setPlaying(false);
          setMode(null);
        }}
      />
    </div>
  );
}
