import { z } from 'zod';
import { PRICE_RANGES } from './constants';

export const loginSchema = z.object({
  email: z.email(),
  password: z.string().min(1, 'Password is required'),
});

export const categorySchema = z.object({
  code: z.string().min(1, 'Code is required'),
  name: z.string().min(1, 'Name is required'),
  description: z.string().optional(),
  iconUrl: z.string().optional(),
  sortOrder: z.number().min(0),
  isActive: z.boolean(),
});

export const poiSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  description: z.string().min(1, 'Description is required'),
  categoryId: z.string().min(1, 'Category is required'),
  address: z.string().min(1, 'Address is required'),
  ward: z.string().min(1, 'Ward is required'),
  district: z.string().min(1, 'District is required'),
  city: z.string().min(1, 'City is required'),
  latitude: z.number().min(-90).max(90),
  longitude: z.number().min(-180).max(180),
  priceRange: z.enum(PRICE_RANGES),
  priority: z.number().min(0),
  mapUrl: z.string().optional(),
  ttsScript: z.string().optional(),
  geofenceRadiusMeters: z.number().min(10).max(10000),
  autoNarrationEnabled: z.boolean(),
  tagsText: z.string().optional(),
  ownerId: z.string().optional(),
  isActive: z.boolean(),
  activationRequested: z.boolean(),
  autoTranslateAudioContent: z.boolean(),
  overwriteAutoTranslations: z.boolean(),
  autoTranslateLanguages: z.array(z.string()).default([]),
});

export const localizationSchema = z.object({
  lang: z.string().min(1),
  name: z.string().min(1),
  description: z.string().min(1),
  audioUrl: z.string().optional(),
  ttsScript: z.string().optional(),
  isFallback: z.boolean(),
});

export const audioSchema = z.object({
  lang: z.string().min(1),
  audioUrl: z.string().optional(),
  voiceName: z.string().optional(),
  sourceType: z.string().min(1),
});

export const tourSchema = z.object({
  title: z.string().min(1, 'Title is required'),
  description: z.string().min(1, 'Description is required'),
  lang: z.string().min(1, 'Language is required'),
  coverImageUrl: z.string().optional(),
  estimatedDurationMinutes: z.number().min(1).max(1440),
  isActive: z.boolean(),
  stopsText: z.string().min(1, 'At least one tour stop is required'),
});
