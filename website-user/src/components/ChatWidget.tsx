import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useMutation } from '@tanstack/react-query';
import { LoaderCircle, MapPin, MessageCircle, Navigation, Send, Sparkles, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { suggestChat, type ChatPoiSuggestion } from '../api/chatApi';
import { useAppStore } from '../store/appStore';
import { distance } from '../utils/analytics';
import { normalizeMediaUrl } from '../utils/media';

type ChatRole = 'user' | 'bot';

type ChatMessage = {
  id: string;
  role: ChatRole;
  text: string;
  suggestions?: ChatPoiSuggestion[];
  variant?: 'default' | 'note';
};

const INITIAL_MESSAGES: ChatMessage[] = [
  {
    id: 'welcome',
    role: 'bot',
    text: 'Xin chào! Bạn muốn ăn gì hôm nay?',
  },
];

export function ChatWidget() {
  const { location, setLocation } = useAppStore();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>(INITIAL_MESSAGES);
  const listRef = useRef<HTMLDivElement | null>(null);
  const conversationIdRef = useRef(
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `chat-${Date.now()}`,
  );
  const mutation = useMutation({
    mutationFn: suggestChat,
  });

  useEffect(() => {
    if (!open) {
      return;
    }

    listRef.current?.scrollTo({
      top: listRef.current.scrollHeight,
      behavior: 'smooth',
    });
  }, [messages, mutation.isPending, open]);

  const appendMessage = (message: ChatMessage) => {
    setMessages((current) => [...current, message]);
  };

  const sendMessage = async (rawMessage: string) => {
    const message = rawMessage.trim();
    if (!message || mutation.isPending) {
      return;
    }

    appendMessage({
      id: createMessageId(),
      role: 'user',
      text: message,
    });
    setDraft('');

    const locationContext = await resolveLocationContext(message, location, setLocation);
    if (locationContext.notice) {
      appendMessage({
        id: createMessageId(),
        role: 'bot',
        text: locationContext.notice,
        variant: 'note',
      });
    }

    try {
      const response = await mutation.mutateAsync({
        message,
        latitude: locationContext.latitude,
        longitude: locationContext.longitude,
        conversationId: conversationIdRef.current,
      });

      appendMessage({
        id: createMessageId(),
        role: 'bot',
        text: response.reply,
        suggestions: response.suggestions,
      });
    } catch (error) {
      appendMessage({
        id: createMessageId(),
        role: 'bot',
        text: (error as Error)?.message || 'Mình chưa thể xử lý yêu cầu lúc này. Bạn thử lại sau nhé.',
      });
    }
  };

  return (
    <div className="fixed bottom-5 right-5 z-40 sm:bottom-6 sm:right-6">
      {open ? (
        <div className="flex h-[min(70vh,38rem)] w-[min(25rem,calc(100vw-1.5rem))] flex-col overflow-hidden rounded-[2rem] border border-slate-200 bg-white shadow-2xl dark:border-slate-800 dark:bg-slate-950">
          <div className="flex items-start justify-between gap-3 border-b border-slate-200 bg-gradient-to-br from-coral via-orange-500 to-teal p-5 text-white dark:border-slate-800">
            <div>
              <p className="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-bold uppercase tracking-[0.2em]">
                <Sparkles size={14} />
                Food AI
              </p>
              <h2 className="mt-3 text-xl font-bold">Trợ lý ẩm thực</h2>
              <p className="mt-1 text-sm text-white/85">Gợi ý từ dữ liệu POI thật trong hệ thống.</p>
            </div>

            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-full bg-white/15 p-2 text-white transition hover:bg-white/25"
              aria-label="Đóng chatbot"
            >
              <X size={18} />
            </button>
          </div>

          <div className="flex flex-wrap gap-2 border-b border-slate-200 px-4 py-3 dark:border-slate-800">
            <button
              type="button"
              onClick={() => void sendMessage('Gợi ý quán gần tôi')}
              disabled={mutation.isPending}
              className="pill inline-flex items-center gap-2 border-teal/30 bg-teal/10 text-teal disabled:opacity-60"
            >
              <Navigation size={14} />
              Gợi ý gần tôi
            </button>
            <button
              type="button"
              onClick={() => setDraft('Tối nay ăn gì dưới 100k?')}
              className="pill inline-flex items-center gap-2"
            >
              <Sparkles size={14} />
              Dưới 100k
            </button>
          </div>

          <div ref={listRef} className="flex-1 space-y-4 overflow-y-auto bg-slate-50/80 px-4 py-4 dark:bg-slate-950">
            {messages.map((message) => (
              <div key={message.id} className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                <div className={`max-w-[88%] ${message.role === 'user' ? 'items-end' : 'items-start'} flex flex-col gap-2`}>
                  <div
                    className={`rounded-2xl px-4 py-3 text-sm leading-6 ${
                      message.role === 'user'
                        ? 'bg-coral text-white'
                        : message.variant === 'note'
                          ? 'border border-amber-200 bg-amber-50 text-amber-900'
                          : 'bg-white text-slate-700 shadow-sm dark:bg-slate-900 dark:text-slate-100'
                    }`}
                  >
                    {message.text}
                  </div>

                  {message.suggestions?.length ? (
                    <div className="space-y-3">
                      {message.suggestions.map((suggestion) => (
                        <SuggestionCard
                          key={`${message.id}-${suggestion.poiId}`}
                          suggestion={suggestion}
                          onNavigate={() => setOpen(false)}
                        />
                      ))}
                    </div>
                  ) : null}
                </div>
              </div>
            ))}

            {mutation.isPending ? (
              <div className="flex justify-start">
                <div className="inline-flex items-center gap-2 rounded-2xl bg-white px-4 py-3 text-sm text-slate-600 shadow-sm dark:bg-slate-900 dark:text-slate-200">
                  <LoaderCircle size={16} className="animate-spin" />
                  Đang tìm địa điểm phù hợp...
                </div>
              </div>
            ) : null}
          </div>

          <form
            className="border-t border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-950"
            onSubmit={(event) => {
              event.preventDefault();
              void sendMessage(draft);
            }}
          >
            <label className="mb-2 block text-xs font-bold uppercase tracking-[0.18em] text-slate-500">
              Nhập nhu cầu của bạn
            </label>
            <div className="flex items-center gap-2">
              <input
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                placeholder="Ví dụ: Gợi ý quán ốc gần tôi"
                maxLength={500}
                className="min-w-0 flex-1 rounded-full border border-slate-200 bg-slate-50 px-4 py-3 text-sm outline-none transition focus:border-coral focus:bg-white dark:border-slate-700 dark:bg-slate-900"
              />
              <button
                type="submit"
                disabled={mutation.isPending || !draft.trim()}
                className="btn-primary h-12 w-12 shrink-0 rounded-full !px-0 !py-0 disabled:cursor-not-allowed disabled:opacity-60"
                aria-label="Gửi tin nhắn"
              >
                <Send size={18} />
              </button>
            </div>
          </form>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex items-center gap-3 rounded-full bg-coral px-5 py-3 font-semibold text-white shadow-2xl transition hover:-translate-y-0.5 hover:bg-orange-600"
        >
          <MessageCircle size={20} />
          Trợ lý ẩm thực
        </button>
      )}
    </div>
  );
}

function SuggestionCard({
  suggestion,
  onNavigate,
}: {
  suggestion: ChatPoiSuggestion;
  onNavigate: () => void;
}) {
  const imageUrl = normalizeMediaUrl(suggestion.imageUrl);
  const detailTarget = suggestion.detailUrl || `/poi/${suggestion.poiId}`;
  const mapTarget = suggestion.mapUrl || `/map?poi=${suggestion.poiId}`;

  return (
    <article className="overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <img src={imageUrl} alt={suggestion.name} className="h-32 w-full object-cover" />

      <div className="space-y-3 p-4">
        <div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white">{suggestion.name}</h3>
          <p className="mt-1 flex items-start gap-2 text-sm text-slate-500 dark:text-slate-300">
            <MapPin size={15} className="mt-0.5 shrink-0 text-coral" />
            <span>
              {[suggestion.address, suggestion.ward].filter(Boolean).join(', ') || 'Đang cập nhật địa chỉ'}
            </span>
          </p>
        </div>

        {suggestion.reason ? (
          <p className="rounded-2xl bg-slate-50 px-3 py-2 text-sm leading-6 text-slate-600 dark:bg-slate-800 dark:text-slate-200">
            {suggestion.reason}
          </p>
        ) : null}

        <div className="flex flex-wrap gap-2">
          {typeof suggestion.distanceMeters === 'number' ? (
            <span className="pill border-teal/30 bg-teal/10 text-teal">{distance(suggestion.distanceMeters)}</span>
          ) : null}
        </div>

        <div className="flex flex-wrap gap-2">
          <SmartLink
            to={detailTarget}
            onClick={onNavigate}
            className="btn-primary !px-4 !py-2 text-sm"
          >
            Xem chi tiết
          </SmartLink>
          <SmartLink
            to={mapTarget}
            onClick={onNavigate}
            className="btn-secondary !px-4 !py-2 text-sm"
          >
            Chỉ đường
          </SmartLink>
        </div>
      </div>
    </article>
  );
}

function SmartLink({
  to,
  onClick,
  className,
  children,
}: {
  to: string;
  onClick: () => void;
  className: string;
  children: ReactNode;
}) {
  if (/^https?:\/\//i.test(to)) {
    return (
      <a href={to} target="_blank" rel="noreferrer" onClick={onClick} className={className}>
        {children}
      </a>
    );
  }

  return (
    <Link to={to} onClick={onClick} className={className}>
      {children}
    </Link>
  );
}

async function resolveLocationContext(
  message: string,
  location: { lat: number; lng: number } | undefined,
  setLocation: (location: { lat: number; lng: number }) => void,
): Promise<{ latitude?: number; longitude?: number; notice?: string }> {
  if (!shouldUseLocation(message)) {
    return {};
  }

  if (location) {
    return {
      latitude: location.lat,
      longitude: location.lng,
    };
  }

  if (!navigator.geolocation) {
    return {
      notice: 'Bạn chưa bật vị trí nên mình sẽ gợi ý theo dữ liệu hiện có.',
    };
  }

  try {
    const position = await getCurrentPosition();
    const nextLocation = {
      lat: position.coords.latitude,
      lng: position.coords.longitude,
    };
    setLocation(nextLocation);
    return {
      latitude: nextLocation.lat,
      longitude: nextLocation.lng,
    };
  } catch {
    return {
      notice: 'Bạn chưa bật vị trí nên mình sẽ gợi ý theo dữ liệu hiện có.',
    };
  }
}

function shouldUseLocation(message: string) {
  const normalized = message
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '');

  return normalized.includes('gan toi') || normalized.includes('gan day') || normalized.includes('near');
}

function getCurrentPosition(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: true,
      timeout: 10000,
      maximumAge: 10000,
    });
  });
}

function createMessageId() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `chat-message-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
