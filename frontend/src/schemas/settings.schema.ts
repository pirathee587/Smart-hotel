import { z } from "zod";

export const profileFormSchema = z.object({
  fullName: z.string().min(2, "Full name must be at least 2 characters").max(100),
  phone: z.string().min(5, "Please enter a valid phone number").max(25),
  nationality: z.string().min(2, "Please select your nationality"),
  avatarUrl: z.string().optional(),
});

export type ProfileFormValues = z.infer<typeof profileFormSchema>;

export const passwordFormSchema = z
  .object({
    currentPassword: z.string().min(1, "Current password is required"),
    newPassword: z
      .string()
      .min(8, "New password must be at least 8 characters long")
      .regex(/[a-z]/, "Password must include lowercase letters")
      .regex(/[A-Z]/, "Password must include uppercase letters")
      .regex(/\d/, "Password must include at least one number"),
    confirmPassword: z.string().min(1, "Please confirm your new password"),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "New passwords do not match",
    path: ["confirmPassword"],
  });

export type PasswordFormValues = z.infer<typeof passwordFormSchema>;

export const currencyPreferenceSchema = z.object({
  currency: z.enum(["USD", "LKR"]),
});

export type CurrencyPreferenceValues = z.infer<typeof currencyPreferenceSchema>;

export const reviewEditSchema = z.object({
  rating: z.number().min(1).max(5),
  text: z.string().min(5, "Review must be at least 5 characters").max(1000),
});

export type ReviewEditValues = z.infer<typeof reviewEditSchema>;

export const notificationPreferencesSchema = z.object({
  bookingUpdates: z.boolean(),
  serviceRequestUpdates: z.boolean(),
  promotions: z.boolean(),
});

export type NotificationPreferencesValues = z.infer<typeof notificationPreferencesSchema>;
