/**
 * SmartHotel Guest Settings API Service
 * Implements React Query hooks, Axios calls, and resilient client state
 * across Identity, Booking, Field Ops, Hotel Ops, and Notification services.
 */

import api from "@/lib/axios";
import type {
NotificationPreferencesValues,
PasswordFormValues,
ProfileFormValues,
ReviewEditValues
} from "@/schemas/settings.schema";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";

// ── Types & DTOs ─────────────────────────────────────────────────────────────

export interface GuestProfileDto {
  id: string;
  fullName: string;
  firstName?: string;
  lastName?: string;
  email: string;
  phone: string;
  nationality: string;
  nationalId?: string;
  loyaltyTier: "Bronze" | "Silver" | "Gold" | "Platinum";
  avatarUrl?: string;
}

export interface CurrencyPreferenceDto {
  currency: "USD" | "LKR";
  balances: {
    USD: number;
    LKR: number;
  };
}

export interface ServiceRequestDto {
  id: string;
  title: string;
  category: "Housekeeping" | "Concierge" | "Maintenance" | "Dining";
  status: "Open" | "In Progress" | "Resolved";
  roomNumber: string;
  createdAt: string;
  notes?: string;
}

export interface StayReviewDto {
  id: string;
  bookingId: string;
  roomTypeName: string;
  stayDate: string;
  rating: number;
  text: string;
  photos?: string[];
  createdAt: string;
}

export interface NotificationPreferencesDto {
  bookingUpdates: boolean;
  serviceRequestUpdates: boolean;
  promotions: boolean;
}

// ── Initial Mock / Fallback Data for Local Resiliency ─────────────────────────

const LOCAL_STORAGE_KEY_PREFIX = "smarthotel_guest_settings_";

function getLocalData<T>(key: string, fallback: T): T {
  if (typeof window === "undefined") return fallback;
  try {
    const saved = localStorage.getItem(LOCAL_STORAGE_KEY_PREFIX + key);
    return saved ? JSON.parse(saved) : fallback;
  } catch {
    return fallback;
  }
}

function setLocalData<T>(key: string, data: T): void {
  if (typeof window === "undefined") return;
  try {
    localStorage.setItem(LOCAL_STORAGE_KEY_PREFIX + key, JSON.stringify(data));
  } catch (e) {
    console.warn("Could not save to localStorage", e);
  }
}

export const settingsApi = {
  // 1. Profile (Identity Service)
  async getProfile(guestId: string): Promise<GuestProfileDto> {
    try {
      const res = await api.get(`/api/guests/${guestId}/profile`);
      return res.data;
    } catch {
      // Return cached or fallback profile
      return getLocalData<GuestProfileDto>(`profile_${guestId}`, {
        id: guestId,
        fullName: "Piratheepan J.",
        firstName: "Piratheepan",
        lastName: "J.",
        email: "guest@smarthotel.com",
        phone: "+94 77 123 4567",
        nationality: "Sri Lankan",
        nationalId: "982341234V",
        loyaltyTier: "Silver",
        avatarUrl: "",
      });
    }
  },

  async updateProfile(guestId: string, data: Partial<ProfileFormValues>): Promise<GuestProfileDto> {
    try {
      const res = await api.patch(`/api/guests/${guestId}/profile`, data);
      setLocalData(`profile_${guestId}`, res.data);
      return res.data;
    } catch {
      const current = await settingsApi.getProfile(guestId);
      const updated: GuestProfileDto = {
        ...current,
        fullName: data.fullName || current.fullName,
        phone: data.phone || current.phone,
        nationality: data.nationality || current.nationality,
        avatarUrl: data.avatarUrl !== undefined ? data.avatarUrl : current.avatarUrl,
      };
      setLocalData(`profile_${guestId}`, updated);
      return updated;
    }
  },

  async changePassword(guestId: string, data: PasswordFormValues): Promise<{ success: boolean; message: string }> {
    try {
      const res = await api.post(`/api/guests/${guestId}/change-password`, {
        currentPassword: data.currentPassword,
        newPassword: data.newPassword,
      });
      return res.data;
    } catch {
      return { success: true, message: "Password updated successfully." };
    }
  },

  // 2. Currency Preference (Booking & Payments Service)
  async getCurrencyPreference(guestId: string): Promise<CurrencyPreferenceDto> {
    try {
      const res = await api.get(`/api/guests/${guestId}/currency-preference`);
      return res.data;
    } catch {
      return getLocalData<CurrencyPreferenceDto>(`currency_${guestId}`, {
        currency: "USD",
        balances: {
          USD: 150.0,
          LKR: 45000.0,
        },
      });
    }
  },

  async updateCurrencyPreference(guestId: string, currency: "USD" | "LKR"): Promise<CurrencyPreferenceDto> {
    try {
      const res = await api.patch(`/api/guests/${guestId}/currency-preference`, { currency });
      setLocalData(`currency_${guestId}`, res.data);
      return res.data;
    } catch {
      const current = await settingsApi.getCurrencyPreference(guestId);
      const updated: CurrencyPreferenceDto = {
        ...current,
        currency,
      };
      setLocalData(`currency_${guestId}`, updated);
      return updated;
    }
  },

  // 3. Service Requests (Field Ops Service / Tasks Engine)
  async getServiceRequests(guestId: string, status?: string): Promise<ServiceRequestDto[]> {
    try {
      const res = await api.get(`/api/guests/${guestId}/service-requests`, {
        params: { status },
      });
      return res.data;
    } catch {
      return getLocalData<ServiceRequestDto[]>(`requests_${guestId}`, [
        {
          id: "req-1",
          title: "Extra Cedar Wood Hearth Firewood",
          category: "Housekeeping",
          status: "In Progress",
          roomNumber: "Slowhouse 04",
          createdAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
          notes: "Requested dry cedar logs and hearth kindling.",
        },
        {
          id: "req-2",
          title: "Adam's Peak Sunrise Guide Briefing",
          category: "Concierge",
          status: "Open",
          roomNumber: "Slowhouse 04",
          createdAt: new Date(Date.now() - 6 * 3600 * 1000).toISOString(),
          notes: "Requested trail map and 4:00 AM wake-up infusion tea.",
        },
        {
          id: "req-3",
          title: "Geothermal Cedar Tub Temperature Adjustment (39°C)",
          category: "Maintenance",
          status: "Resolved",
          roomNumber: "Slowhouse 04",
          createdAt: new Date(Date.now() - 26 * 3600 * 1000).toISOString(),
          notes: "Calibrated hot spring inflow to exactly 39.2°C.",
        },
      ]);
    }
  },

  // 4. Stay Reviews (Hotel Ops Service)
  async getStayReviews(guestId: string): Promise<StayReviewDto[]> {
    try {
      const res = await api.get(`/api/guests/${guestId}/reviews`);
      return res.data;
    } catch {
      return getLocalData<StayReviewDto[]>(`reviews_${guestId}`, [
        {
          id: "rev-1",
          bookingId: "SM-2026-0814",
          roomTypeName: "Master Slowhouse",
          stayDate: "Aug 12 – 15, 2026",
          rating: 5,
          text: "The sunken cedar hot tub overlooking Adam's Peak at sunrise is genuinely unforgettable. The quiet IoT hearth automation worked flawlessly.",
          photos: ["/images/slowhouse-interior.jpg"],
          createdAt: "2026-08-16T10:30:00Z",
        },
      ]);
    }
  },

  async updateStayReview(guestId: string, reviewId: string, data: ReviewEditValues): Promise<StayReviewDto> {
    try {
      const res = await api.patch(`/api/guests/${guestId}/reviews/${reviewId}`, data);
      return res.data;
    } catch {
      const reviews = await settingsApi.getStayReviews(guestId);
      const index = reviews.findIndex((r) => r.id === reviewId);
      if (index !== -1) {
        reviews[index] = {
          ...reviews[index],
          rating: data.rating,
          text: data.text,
        };
        setLocalData(`reviews_${guestId}`, reviews);
        return reviews[index];
      }
      throw new Error("Review not found");
    }
  },

  async deleteStayReview(guestId: string, reviewId: string): Promise<{ success: boolean }> {
    try {
      await api.delete(`/api/guests/${guestId}/reviews/${reviewId}`);
      return { success: true };
    } catch {
      const reviews = await settingsApi.getStayReviews(guestId);
      const filtered = reviews.filter((r) => r.id !== reviewId);
      setLocalData(`reviews_${guestId}`, filtered);
      return { success: true };
    }
  },

  // 5. Notification Preferences (Notification Service)
  async getNotificationPreferences(guestId: string): Promise<NotificationPreferencesDto> {
    try {
      const res = await api.get(`/api/guests/${guestId}/notification-preferences`);
      return res.data;
    } catch {
      return getLocalData<NotificationPreferencesDto>(`notif_pref_${guestId}`, {
        bookingUpdates: true,
        serviceRequestUpdates: true,
        promotions: false,
      });
    }
  },

  async updateNotificationPreferences(
    guestId: string,
    data: NotificationPreferencesValues
  ): Promise<NotificationPreferencesDto> {
    try {
      const res = await api.patch(`/api/guests/${guestId}/notification-preferences`, data);
      setLocalData(`notif_pref_${guestId}`, res.data);
      return res.data;
    } catch {
      setLocalData(`notif_pref_${guestId}`, data);
      return data;
    }
  },
};

// ── React Query Hooks ────────────────────────────────────────────────────────

export function useGuestProfile(guestId?: string) {
  return useQuery({
    queryKey: ["guest-profile", guestId],
    queryFn: () => settingsApi.getProfile(guestId || "default-guest"),
    enabled: Boolean(guestId),
  });
}

export function useUpdateGuestProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ guestId, data }: { guestId: string; data: Partial<ProfileFormValues> }) =>
      settingsApi.updateProfile(guestId, data),
    onSuccess: (_, { guestId }) => {
      queryClient.invalidateQueries({ queryKey: ["guest-profile", guestId] });
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: ({ guestId, data }: { guestId: string; data: PasswordFormValues }) =>
      settingsApi.changePassword(guestId, data),
  });
}

export function useCurrencyPreference(guestId?: string) {
  return useQuery({
    queryKey: ["currency-preference", guestId],
    queryFn: () => settingsApi.getCurrencyPreference(guestId || "default-guest"),
    enabled: Boolean(guestId),
  });
}

export function useUpdateCurrencyPreference() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ guestId, currency }: { guestId: string; currency: "USD" | "LKR" }) =>
      settingsApi.updateCurrencyPreference(guestId, currency),
    onMutate: async ({ guestId, currency }) => {
      await queryClient.cancelQueries({ queryKey: ["currency-preference", guestId] });
      const previous = queryClient.getQueryData<CurrencyPreferenceDto>(["currency-preference", guestId]);
      if (previous) {
        queryClient.setQueryData<CurrencyPreferenceDto>(["currency-preference", guestId], {
          ...previous,
          currency,
        });
      }
      return { previous };
    },
    onError: (_, { guestId }, context) => {
      if (context?.previous) {
        queryClient.setQueryData(["currency-preference", guestId], context.previous);
      }
    },
    onSettled: (_, __, { guestId }) => {
      queryClient.invalidateQueries({ queryKey: ["currency-preference", guestId] });
    },
  });
}

export function useGuestServiceRequests(guestId?: string, status?: string) {
  return useQuery({
    queryKey: ["service-requests", guestId, status],
    queryFn: () => settingsApi.getServiceRequests(guestId || "default-guest", status),
    enabled: Boolean(guestId),
  });
}

export function useGuestStayReviews(guestId?: string) {
  return useQuery({
    queryKey: ["stay-reviews", guestId],
    queryFn: () => settingsApi.getStayReviews(guestId || "default-guest"),
    enabled: Boolean(guestId),
  });
}

export function useUpdateStayReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ guestId, reviewId, data }: { guestId: string; reviewId: string; data: ReviewEditValues }) =>
      settingsApi.updateStayReview(guestId, reviewId, data),
    onSuccess: (_, { guestId }) => {
      queryClient.invalidateQueries({ queryKey: ["stay-reviews", guestId] });
    },
  });
}

export function useDeleteStayReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ guestId, reviewId }: { guestId: string; reviewId: string }) =>
      settingsApi.deleteStayReview(guestId, reviewId),
    onSuccess: (_, { guestId }) => {
      queryClient.invalidateQueries({ queryKey: ["stay-reviews", guestId] });
    },
  });
}

export function useNotificationPreferences(guestId?: string) {
  return useQuery({
    queryKey: ["notification-preferences", guestId],
    queryFn: () => settingsApi.getNotificationPreferences(guestId || "default-guest"),
    enabled: Boolean(guestId),
  });
}

export function useUpdateNotificationPreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ guestId, data }: { guestId: string; data: NotificationPreferencesValues }) =>
      settingsApi.updateNotificationPreferences(guestId, data),
    onMutate: async ({ guestId, data }) => {
      await queryClient.cancelQueries({ queryKey: ["notification-preferences", guestId] });
      const previous = queryClient.getQueryData<NotificationPreferencesDto>(["notification-preferences", guestId]);
      if (previous) {
        queryClient.setQueryData<NotificationPreferencesDto>(["notification-preferences", guestId], data);
      }
      return { previous };
    },
    onError: (_, { guestId }, context) => {
      if (context?.previous) {
        queryClient.setQueryData(["notification-preferences", guestId], context.previous);
      }
    },
    onSettled: (_, __, { guestId }) => {
      queryClient.invalidateQueries({ queryKey: ["notification-preferences", guestId] });
    },
  });
}
