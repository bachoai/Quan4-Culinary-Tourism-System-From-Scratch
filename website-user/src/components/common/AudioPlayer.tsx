import { Pause, Play, Volume2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

type AudioPlayerProps = {
  text?: string;
  lang: string;
  onPlay: () => void;
  autoplay?: boolean;
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

export function AudioPlayer({ text, lang, onPlay, autoplay = false }: AudioPlayerProps) {
  const autoStarted = useRef(false);
  const utterance = useRef<SpeechSynthesisUtterance | null>(null);
  const [playing, setPlaying] = useState(false);
  const [ttsVoice, setTtsVoice] = useState<SpeechSynthesisVoice | null>(null);
  const [voiceChecked, setVoiceChecked] = useState(false);
  const narrationText = text?.trim() || '';
  const hasSpeechSynthesis = typeof window !== 'undefined' && 'speechSynthesis' in window;
  const hasNarration = Boolean(narrationText);
  const hasVietnameseVoice = Boolean(ttsVoice);
  const canUseTts = hasNarration && hasSpeechSynthesis && hasVietnameseVoice;

  useEffect(() => {
    if (!hasSpeechSynthesis) {
      setTtsVoice(null);
      setVoiceChecked(true);
      return;
    }

    const syncVoice = () => {
      const nextVoice = resolveVoice(lang, window.speechSynthesis.getVoices());
      setTtsVoice(nextVoice);
      setVoiceChecked(true);
    };

    syncVoice();
    window.speechSynthesis.addEventListener('voiceschanged', syncVoice);

    return () => {
      window.speechSynthesis.removeEventListener('voiceschanged', syncVoice);
    };
  }, [hasSpeechSynthesis, lang]);

  useEffect(() => {
    autoStarted.current = false;
    if (hasSpeechSynthesis) {
      window.speechSynthesis.cancel();
    }

    setPlaying(false);
  }, [narrationText, hasSpeechSynthesis, lang]);

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

    if (!voiceChecked && hasNarration && hasSpeechSynthesis) {
      return;
    }

    if (!canUseTts) {
      return;
    }

    autoStarted.current = true;
    void startNarration();
  }, [autoplay, canUseTts, hasNarration, hasSpeechSynthesis, voiceChecked]);

  if (!hasNarration) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        Dia diem nay chua co noi dung thuyet minh.
      </div>
    );
  }

  if (!hasSpeechSynthesis) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        Trinh duyet nay khong ho tro doc thuyet minh.
      </div>
    );
  }

  if (voiceChecked && !hasVietnameseVoice) {
    return (
      <div className="rounded-2xl bg-slate-100 p-4 text-sm text-slate-500 dark:bg-slate-800">
        May nay chua co giong doc tieng Viet, nen web khong phat de tranh doc sai ngon ngu.
      </div>
    );
  }

  const stopNarration = () => {
    if (hasSpeechSynthesis) {
      window.speechSynthesis.cancel();
    }

    setPlaying(false);
  };

  const startTts = async () => {
    if (!canUseTts || !hasSpeechSynthesis) {
      return;
    }

    window.speechSynthesis.cancel();

    const nextUtterance = new SpeechSynthesisUtterance(narrationText);
    nextUtterance.lang = narrationLangMap[lang] ?? lang;
    if (ttsVoice) {
      nextUtterance.voice = ttsVoice;
    }
    nextUtterance.onstart = () => {
      setPlaying(true);
      onPlay();
    };
    nextUtterance.onend = () => {
      setPlaying(false);
    };
    nextUtterance.onerror = () => {
      setPlaying(false);
    };

    utterance.current = nextUtterance;
    window.speechSynthesis.speak(nextUtterance);
  };

  async function startNarration() {
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
          Thuyet minh bang giong doc
        </p>
        <p className="text-xs text-slate-500">
          {voiceChecked
            ? 'Doc tu kich ban thuyet minh bang giong tieng Viet cua trinh duyet.'
            : 'Dang kiem tra giong doc tieng Viet tren trinh duyet.'}
        </p>
      </div>
    </div>
  );
}
