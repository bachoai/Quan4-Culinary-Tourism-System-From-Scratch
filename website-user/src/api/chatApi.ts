import { postData } from './axiosClient';

export type ChatSuggestRequest = {
  message: string;
  latitude?: number;
  longitude?: number;
  conversationId?: string;
};

export type ChatPoiSuggestion = {
  poiId: string;
  name: string;
  address?: string;
  ward?: string;
  imageUrl?: string;
  reason?: string;
  distanceMeters?: number;
  detailUrl?: string;
  mapUrl?: string;
};

export type ChatSuggestResponse = {
  reply: string;
  suggestions: ChatPoiSuggestion[];
};

export function suggestChat(payload: ChatSuggestRequest): Promise<ChatSuggestResponse> {
  return postData<ChatSuggestResponse>('/api/v1/chat/suggest', payload);
}
