"use client";

import { ImageUploadDropzone } from "@/components/ui/ImageUploadDropzone";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import {
passwordFormSchema,
profileFormSchema,
reviewEditSchema,
} from "@/schemas/settings.schema";
import {
useChangePassword,
useCurrencyPreference,
useDeleteStayReview,
useGuestProfile,
useGuestServiceRequests,
useGuestStayReviews,
useNotificationPreferences,
useUpdateCurrencyPreference,
useUpdateGuestProfile,
useUpdateNotificationPreferences,
useUpdateStayReview,
type StayReviewDto,
} from "@/services/settingsApi";
import {
AlertCircle,
Award,
Bell,
Camera,
CheckCircle2,
ChevronDown,
ChevronUp,
Edit2,
Eye,
EyeOff,
Lock,
LogOut,
Mail,
Phone,
Shield,
Sparkles,
Star,
Trash2,
Wallet,
X
} from "lucide-react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import React,{ useState } from "react";

interface GuestSettingsViewProps {
  onClose?: () => void;
  isDrawer?: boolean;
}

const NATIONALITIES = [
  "Sri Lankan",
  "British",
  "German",
  "French",
  "American",
  "Australian",
  "Canadian",
  "Indian",
  "Swiss",
  "Singaporean",
  "Japanese",
  "Other",
];

export default function GuestSettingsView({ onClose, isDrawer = false }: GuestSettingsViewProps) {
  const router = useRouter();
  const { user, logout } = useAuthStore();
  const guestId = user?.id || "guest-default";

  const handleLogout = () => {
    logout();
    if (onClose) {
      onClose();
    }
    router.push("/");
  };

  // Data queries
  const { data: profile, isLoading: isProfileLoading } = useGuestProfile(guestId);
  const { data: currencyData } = useCurrencyPreference(guestId);
  const { data: serviceRequests = [], isLoading: isRequestsLoading } = useGuestServiceRequests(guestId);
  const { data: reviews = [], isLoading: isReviewsLoading } = useGuestStayReviews(guestId);
  const { data: notifications } = useNotificationPreferences(guestId);

  // Mutations
  const updateProfileMutation = useUpdateGuestProfile();
  const changePasswordMutation = useChangePassword();
  const updateCurrencyMutation = useUpdateCurrencyPreference();
  const updateReviewMutation = useUpdateStayReview();
  const deleteReviewMutation = useDeleteStayReview();
  const updateNotifMutation = useUpdateNotificationPreferences();

  // 1. Profile State
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [nationality, setNationality] = useState("Sri Lankan");
  const [profileSuccess, setProfileSuccess] = useState<string | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [showAvatarUpload, setShowAvatarUpload] = useState(false);

  // Sync profile data to local edit form
  React.useEffect(() => {
    if (profile) {
      queueMicrotask(() => {
        setFullName(profile.fullName || user?.name || "");
        setPhone(profile.phone || "");
        setNationality(profile.nationality || "Sri Lankan");
      });
    }
  }, [profile, user]);

  const handleProfileSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setProfileError(null);
    setProfileSuccess(null);

    const validation = profileFormSchema.safeParse({
      fullName,
      phone,
      nationality,
      avatarUrl: profile?.avatarUrl,
    });

    if (!validation.success) {
      setProfileError(validation.error.issues[0]?.message || "Invalid profile data");
      return;
    }

    try {
      await updateProfileMutation.mutateAsync({
        guestId,
        data: {
          fullName,
          phone,
          nationality,
        },
      });
      setProfileSuccess("Profile updated successfully");
      setIsEditingProfile(false);
      setTimeout(() => setProfileSuccess(null), 3000);
    } catch {
      setProfileError("Unable to update profile. Please try again.");
    }
  };

  // 2. Password Accordion State
  const [isPasswordAccordionOpen, setIsPasswordAccordionOpen] = useState(false);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrentPass, setShowCurrentPass] = useState(false);
  const [showNewPass, setShowNewPass] = useState(false);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordError(null);
    setPasswordSuccess(null);

    const validation = passwordFormSchema.safeParse({
      currentPassword,
      newPassword,
      confirmPassword,
    });

    if (!validation.success) {
      setPasswordError(validation.error.issues[0]?.message || "Invalid password details");
      return;
    }

    try {
      await changePasswordMutation.mutateAsync({
        guestId,
        data: { currentPassword, newPassword, confirmPassword },
      });
      setPasswordSuccess("Security password updated successfully");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setTimeout(() => setPasswordSuccess(null), 3000);
    } catch {
      setPasswordError("Failed to update password. Please check your current password.");
    }
  };

  // 3. Review Edit Modal State
  const [editingReview, setEditingReview] = useState<StayReviewDto | null>(null);
  const [reviewRating, setReviewRating] = useState(5);
  const [reviewText, setReviewText] = useState("");
  const [reviewError, setReviewError] = useState<string | null>(null);

  const handleSaveReview = async () => {
    if (!editingReview) return;
    setReviewError(null);

    const validation = reviewEditSchema.safeParse({
      rating: reviewRating,
      text: reviewText,
    });

    if (!validation.success) {
      setReviewError(validation.error.issues[0]?.message || "Invalid review data");
      return;
    }

    await updateReviewMutation.mutateAsync({
      guestId,
      reviewId: editingReview.id,
      data: { rating: reviewRating, text: reviewText },
    });

    setEditingReview(null);
  };

  return (
    <div className="w-full text-[#0F1B1A] font-sans">
      {/* Drawer or Page Header */}
      {isDrawer ? (
        <div className="flex items-center justify-between pb-6 mb-6 border-b border-[#0F1B1A]/10">
          <div>
            <h2
              className="font-serif text-2xl font-semibold text-[#0F1B1A] tracking-tight"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Guest Sanctuary Profile
            </h2>
            <p className="text-xs text-[#0F1B1A]/70 mt-0.5">Manage your personal details, stay preferences, and bookings.</p>
          </div>
          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={handleLogout}
              className="inline-flex items-center gap-2 px-4 py-2 sm:px-4.5 sm:py-2.5 rounded-xl bg-[#C4622D] hover:bg-[#A84515] text-white text-xs sm:text-sm font-semibold tracking-wide transition-all shadow-md hover:shadow-lg cursor-pointer focus:outline-none focus:ring-2 focus:ring-[#C4622D] active:scale-95"
              title="Log Out of Sanctuary"
            >
              <LogOut className="w-4 h-4 text-white" />
              <span>Log Out</span>
            </button>
            {onClose && (
              <button
                type="button"
                onClick={onClose}
                className="p-2 rounded-full text-[#0F1B1A]/60 hover:text-[#0F1B1A] hover:bg-[#0F1B1A]/5 transition-colors focus:outline-none cursor-pointer"
                aria-label="Close Settings Drawer"
              >
                <X className="w-5 h-5" />
              </button>
            )}
          </div>
        </div>
      ) : (
        <div className="flex items-center justify-between pb-6 mb-6 border-b border-[#0F1B1A]/10">
          <span className="text-xs font-semibold uppercase tracking-wider text-[#2F5C52]">
            Sanctuary Profile &amp; Preferences
          </span>
          <button
            type="button"
            onClick={handleLogout}
            className="inline-flex items-center gap-2 px-4 py-2 sm:px-4.5 sm:py-2.5 rounded-xl bg-[#C4622D] hover:bg-[#A84515] text-white text-xs sm:text-sm font-semibold tracking-wide transition-all shadow-md hover:shadow-lg cursor-pointer focus:outline-none focus:ring-2 focus:ring-[#C4622D] active:scale-95"
            title="Log Out of Sanctuary"
          >
            <LogOut className="w-4 h-4 text-white" />
            <span>Log Out</span>
          </button>
        </div>
      )}

      <div className="flex flex-col gap-6">
        {/* ─────────────────────────────────────────────────────────────
            1. PROFILE HEADER CARD
        ───────────────────────────────────────────────────────────── */}
        <section
          id="profile-header-card"
          className="p-6 sm:p-7 rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm relative overflow-hidden"
        >
          {isProfileLoading ? (
            <div className="flex items-center gap-4 animate-pulse">
              <div className="w-20 h-20 rounded-full bg-[#0F1B1A]/10" />
              <div className="space-y-2 flex-1">
                <div className="h-5 w-40 bg-[#0F1B1A]/10 rounded" />
                <div className="h-4 w-60 bg-[#0F1B1A]/10 rounded" />
              </div>
            </div>
          ) : (
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-5">
              {/* Avatar + Main Identity */}
              <div className="flex items-center gap-4.5">
                <div className="relative group">
                  <div className="w-18 h-18 sm:w-20 sm:h-20 rounded-full overflow-hidden border-2 border-[#2F5C52]/30 bg-[#F7F6F2] flex items-center justify-center shrink-0 shadow-inner">
                    {profile?.avatarUrl ? (
                      <Image
                        src={profile.avatarUrl}
                        alt={profile.fullName || "Guest"}
                        width={80}
                        height={80}
                        className="w-full h-full object-cover"
                      />
                    ) : (
                      <span className="font-serif text-2xl sm:text-3xl text-[#C4622D] font-medium">
                        {(profile?.fullName || user?.name || "P").charAt(0).toUpperCase()}
                      </span>
                    )}
                  </div>
                  <button
                    type="button"
                    onClick={() => setShowAvatarUpload(!showAvatarUpload)}
                    className="absolute -bottom-1 -right-1 p-1.5 rounded-full bg-[#0F1B1A] hover:bg-[#C4622D] text-white transition-colors shadow cursor-pointer"
                    title="Change profile avatar"
                    aria-label="Upload Avatar"
                  >
                    <Camera className="w-3.5 h-3.5" />
                  </button>
                </div>

                <div className="flex flex-col gap-1">
                  <div className="flex items-center gap-2.5">
                    <h3
                      className="font-serif text-xl sm:text-2xl text-[#0F1B1A] font-semibold tracking-tight"
                      style={{ fontFamily: "var(--font-fraunces), serif" }}
                    >
                      {profile?.fullName || user?.name || "Valued Guest"}
                    </h3>
                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-[#2F5C52]/10 border border-[#2F5C52]/20 text-[#2F5C52] text-[10px] font-semibold tracking-wider uppercase">
                      <Award className="w-3 h-3 text-[#E07A3E]" />
                      {profile?.loyaltyTier || "Silver"}
                    </span>
                  </div>

                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-[#0F1B1A]/70">
                    <span className="inline-flex items-center gap-1">
                      <Mail className="w-3.5 h-3.5 text-[#2F5C52]" />
                      <span>{profile?.email || user?.email || "guest@smarthotel.com"}</span>
                      <span title="Email is locked">
                        <Lock className="w-3 h-3 text-[#0F1B1A]/40" />
                      </span>
                    </span>
                    {profile?.phone && (
                      <span className="inline-flex items-center gap-1">
                        <Phone className="w-3.5 h-3.5 text-[#2F5C52]" />
                        <span>{profile.phone}</span>
                      </span>
                    )}
                  </div>
                </div>
              </div>

              {/* Edit Profile Action Button */}
              <button
                type="button"
                onClick={() => setIsEditingProfile(!isEditingProfile)}
                className="self-start sm:self-center inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-[#0F1B1A] hover:bg-[#16302C] text-[#F7F6F2] text-xs font-medium tracking-wide transition-all cursor-pointer shadow-sm focus:outline-none focus:ring-2 focus:ring-[#C4622D]"
              >
                <Edit2 className="w-3.5 h-3.5 text-[#E07A3E]" />
                <span>{isEditingProfile ? "Close Editor" : "Edit Profile"}</span>
              </button>
            </div>
          )}

          {/* Avatar Dropzone Modal / Drawer Expand */}
          {showAvatarUpload && (
            <div className="mt-5 pt-5 border-t border-[#0F1B1A]/10">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-medium text-[#0F1B1A]">Upload Profile Photo</span>
                <button
                  type="button"
                  onClick={() => setShowAvatarUpload(false)}
                  className="text-[11px] text-[#0F1B1A]/60 hover:text-[#0F1B1A]"
                >
                  Cancel
                </button>
              </div>
              <ImageUploadDropzone
                category="guests"
                onUploadComplete={async (url) => {
                  await updateProfileMutation.mutateAsync({
                    guestId,
                    data: { avatarUrl: url },
                  });
                  setShowAvatarUpload(false);
                }}
              />
            </div>
          )}
        </section>

        {/* ─────────────────────────────────────────────────────────────
            2. EDITABLE FIELDS GRID (Conditionally visible or inline editor)
        ───────────────────────────────────────────────────────────── */}
        {isEditingProfile && (
          <section
            id="edit-profile-section"
            className="p-6 rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm animate-in fade-in duration-200"
          >
            <h4
              className="font-serif text-lg font-semibold text-[#0F1B1A] tracking-tight mb-4"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Update Guest Information
            </h4>

            {profileError && (
              <div className="mb-4 p-3 rounded-xl bg-red-50 border border-red-200 text-red-700 text-xs flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 shrink-0" />
                <span>{profileError}</span>
              </div>
            )}
            {profileSuccess && (
              <div className="mb-4 p-3 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                <span>{profileSuccess}</span>
              </div>
            )}

            <form onSubmit={handleProfileSubmit} className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {/* Full Name */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[#0F1B1A]/80">Full Name</label>
                <input
                  type="text"
                  required
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className="w-full h-10 px-3.5 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs sm:text-sm focus:outline-none focus:bg-white focus:border-[#C4622D]"
                />
              </div>

              {/* Email (Disabled / Locked) */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[#0F1B1A]/60 flex items-center justify-between">
                  <span>Email (Locked)</span>
                  <Lock className="w-3 h-3 text-[#0F1B1A]/40" />
                </label>
                <input
                  type="email"
                  disabled
                  value={profile?.email || user?.email || "guest@smarthotel.com"}
                  className="w-full h-10 px-3.5 rounded-xl bg-[#0F1B1A]/5 border border-[#0F1B1A]/10 text-[#0F1B1A]/50 text-xs sm:text-sm cursor-not-allowed select-none"
                />
              </div>

              {/* Phone Number */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[#0F1B1A]/80">Phone Number</label>
                <input
                  type="tel"
                  required
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="+94 77 123 4567"
                  className="w-full h-10 px-3.5 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs sm:text-sm focus:outline-none focus:bg-white focus:border-[#C4622D]"
                />
              </div>

              {/* Nationality */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[#0F1B1A]/80">Nationality</label>
                <select
                  value={nationality}
                  onChange={(e) => setNationality(e.target.value)}
                  className="w-full h-10 px-3.5 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs sm:text-sm focus:outline-none focus:bg-white focus:border-[#C4622D] cursor-pointer"
                >
                  {NATIONALITIES.map((nat) => (
                    <option key={nat} value={nat} className="bg-white text-[#0F1B1A]">
                      {nat}
                    </option>
                  ))}
                </select>
              </div>

              <div className="sm:col-span-2 flex items-center justify-end gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setIsEditingProfile(false)}
                  className="px-4 py-2 rounded-xl text-xs font-medium text-[#0F1B1A]/70 hover:text-[#0F1B1A] transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={updateProfileMutation.isPending}
                  className="px-5 py-2 rounded-xl bg-[#C4622D] hover:bg-[#E07A3E] text-white text-xs font-medium tracking-wide transition-all shadow-sm cursor-pointer disabled:opacity-50"
                >
                  {updateProfileMutation.isPending ? "Saving Profile..." : "Save Changes"}
                </button>
              </div>
            </form>
          </section>
        )}

        {/* ─────────────────────────────────────────────────────────────
            3. SECURITY & PASSWORD (Collapsible Accordion)
        ───────────────────────────────────────────────────────────── */}
        <section
          id="security-accordion-card"
          className="rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm overflow-hidden"
        >
          <button
            type="button"
            onClick={() => setIsPasswordAccordionOpen(!isPasswordAccordionOpen)}
            className="w-full p-6 flex items-center justify-between text-left hover:bg-[#0F1B1A]/[0.02] transition-colors focus:outline-none cursor-pointer"
          >
            <div className="flex items-center gap-3">
              <div className="p-2.5 rounded-xl bg-[#2F5C52]/10 border border-[#2F5C52]/20 text-[#2F5C52]">
                <Shield className="w-4 h-4 text-[#C4622D]" />
              </div>
              <div>
                <h4
                  className="font-serif text-base sm:text-lg text-[#0F1B1A] font-semibold tracking-tight"
                  style={{ fontFamily: "var(--font-fraunces), serif" }}
                >
                  Security &amp; Password
                </h4>
                <p className="text-xs text-[#0F1B1A]/70">Manage access passkeys and secure authentication credentials.</p>
              </div>
            </div>
            {isPasswordAccordionOpen ? (
              <ChevronUp className="w-5 h-5 text-[#0F1B1A]/60" />
            ) : (
              <ChevronDown className="w-5 h-5 text-[#0F1B1A]/60" />
            )}
          </button>

          {isPasswordAccordionOpen && (
            <div className="p-6 pt-0 border-t border-[#0F1B1A]/8 mt-2 animate-in fade-in duration-200">
              {passwordError && (
                <div className="mb-4 p-3 rounded-xl bg-red-50 border border-red-200 text-red-700 text-xs flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 text-red-500 shrink-0" />
                  <span>{passwordError}</span>
                </div>
              )}
              {passwordSuccess && (
                <div className="mb-4 p-3 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                  <span>{passwordSuccess}</span>
                </div>
              )}

              <form onSubmit={handlePasswordSubmit} className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                {/* Current Password */}
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-medium text-[#0F1B1A]/80">Current Password</label>
                  <div className="relative">
                    <input
                      type={showCurrentPass ? "text" : "password"}
                      required
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder="••••••••"
                      className="w-full h-10 pl-3.5 pr-10 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs sm:text-sm focus:outline-none focus:bg-white focus:border-[#C4622D]"
                    />
                    <button
                      type="button"
                      onClick={() => setShowCurrentPass(!showCurrentPass)}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-[#0F1B1A]/50 hover:text-[#0F1B1A]"
                    >
                      {showCurrentPass ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                </div>

                {/* New Password */}
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-medium text-[#0F1B1A]/80">New Password</label>
                  <div className="relative">
                    <input
                      type={showNewPass ? "text" : "password"}
                      required
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="8+ characters"
                      className="w-full h-10 pl-3.5 pr-10 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs sm:text-sm focus:outline-none focus:bg-white focus:border-[#C4622D]"
                    />
                    <button
                      type="button"
                      onClick={() => setShowNewPass(!showNewPass)}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-[#0F1B1A]/50 hover:text-[#0F1B1A]"
                    >
                      {showNewPass ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                </div>

                {/* Confirm Password */}
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-medium text-[#0F1B1A]/80">Confirm Password</label>
                  <input
                    type="password"
                    required
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="Repeat password"
                    className="w-full h-10 px-3.5 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs sm:text-sm focus:outline-none focus:bg-white focus:border-[#C4622D]"
                  />
                </div>

                <div className="sm:col-span-3 flex justify-end pt-2">
                  <button
                    type="submit"
                    disabled={changePasswordMutation.isPending}
                    className="px-5 py-2 rounded-xl bg-[#C4622D] hover:bg-[#E07A3E] text-white text-xs font-medium tracking-wide transition-all shadow-sm cursor-pointer disabled:opacity-50"
                  >
                    {changePasswordMutation.isPending ? "Updating Password..." : "Update Password"}
                  </button>
                </div>
              </form>
            </div>
          )}
        </section>

        {/* ─────────────────────────────────────────────────────────────
            4. CURRENCY PREFERENCE (Radio Card Selector)
        ───────────────────────────────────────────────────────────── */}
        <section
          id="currency-preference-card"
          className="p-6 rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm"
        >
          <div className="flex items-center justify-between mb-4">
            <div>
              <h4
                className="font-serif text-base sm:text-lg text-[#0F1B1A] font-semibold tracking-tight"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                Currency Preference &amp; Sanctuary Wallet
              </h4>
              <p className="text-xs text-[#0F1B1A]/70">Select your billing currency and view current guest credit balance.</p>
            </div>
            <Wallet className="w-5 h-5 text-[#C4622D]" />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-2">
            {/* USD Selector Card */}
            <div
              onClick={() => updateCurrencyMutation.mutate({ guestId, currency: "USD" })}
              className={`p-4 rounded-xl border transition-all cursor-pointer flex flex-col justify-between gap-3 ${
                currencyData?.currency === "USD"
                  ? "bg-[#2F5C52]/5 border-2 border-[#2F5C52] shadow-sm"
                  : "bg-[#F7F6F2] border-[#0F1B1A]/10 hover:border-[#2F5C52]/40"
              }`}
            >
              <div className="flex items-center justify-between">
                <span className="font-semibold text-sm text-[#0F1B1A] flex items-center gap-1.5">
                  <span className={`w-2.5 h-2.5 rounded-full ${currencyData?.currency === "USD" ? "bg-[#C4622D]" : "bg-gray-400"}`} />
                  USD — US Dollar
                </span>
                <span className="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded bg-[#2F5C52]/10 text-[#2F5C52]">
                  Global
                </span>
              </div>
              <div>
                <span className="text-[11px] text-[#0F1B1A]/60 block">Wallet Balance</span>
                <span className="font-serif text-lg text-[#0F1B1A] font-semibold">
                  ${currencyData?.balances?.USD?.toFixed(2) || "150.00"}
                </span>
              </div>
            </div>

            {/* LKR Selector Card */}
            <div
              onClick={() => updateCurrencyMutation.mutate({ guestId, currency: "LKR" })}
              className={`p-4 rounded-xl border transition-all cursor-pointer flex flex-col justify-between gap-3 ${
                currencyData?.currency === "LKR"
                  ? "bg-[#2F5C52]/5 border-2 border-[#2F5C52] shadow-sm"
                  : "bg-[#F7F6F2] border-[#0F1B1A]/10 hover:border-[#2F5C52]/40"
              }`}
            >
              <div className="flex items-center justify-between">
                <span className="font-semibold text-sm text-[#0F1B1A] flex items-center gap-1.5">
                  <span className={`w-2.5 h-2.5 rounded-full ${currencyData?.currency === "LKR" ? "bg-[#C4622D]" : "bg-gray-400"}`} />
                  LKR — Sri Lankan Rupee
                </span>
                <span className="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded bg-[#2F5C52]/10 text-[#2F5C52]">
                  Local Rate
                </span>
              </div>
              <div>
                <span className="text-[11px] text-[#0F1B1A]/60 block">Wallet Balance</span>
                <span className="font-serif text-lg text-[#0F1B1A] font-semibold">
                  Rs. {currencyData?.balances?.LKR?.toLocaleString() || "45,000"}
                </span>
              </div>
            </div>
          </div>
        </section>

        {/* ─────────────────────────────────────────────────────────────
            5. MY SERVICE REQUESTS (AI Concierge & Task Engine)
        ───────────────────────────────────────────────────────────── */}
        <section
          id="service-requests-card"
          className="p-6 rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm"
        >
          <div className="flex items-center justify-between mb-4">
            <div>
              <h4
                className="font-serif text-base sm:text-lg text-[#0F1B1A] font-semibold tracking-tight"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                My Service Requests
              </h4>
              <p className="text-xs text-[#0F1B1A]/70">Requests dispatched via AI Concierge and Field Operations.</p>
            </div>
            <span className="text-xs text-[#C4622D] font-medium hover:underline cursor-pointer">
              See Full History →
            </span>
          </div>

          {isRequestsLoading ? (
            <div className="space-y-2 animate-pulse">
              <div className="h-14 bg-[#F7F6F2] rounded-xl" />
              <div className="h-14 bg-[#F7F6F2] rounded-xl" />
            </div>
          ) : serviceRequests.length === 0 ? (
            <p className="text-xs text-[#0F1B1A]/50 italic py-3 text-center">No active service requests recorded.</p>
          ) : (
            <div className="flex flex-col gap-2.5">
              {serviceRequests.map((req) => (
                <div
                  key={req.id}
                  className="p-3.5 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/8 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5 transition-colors"
                >
                  <div className="flex items-start gap-3">
                    <div className="p-2 rounded-lg bg-white border border-[#0F1B1A]/8 text-[#C4622D] shrink-0 mt-0.5">
                      <Sparkles className="w-3.5 h-3.5" />
                    </div>
                    <div>
                      <span className="text-xs font-semibold text-[#0F1B1A] block">{req.title}</span>
                      <div className="flex items-center gap-2 text-[11px] text-[#0F1B1A]/65 mt-0.5">
                        <span>{req.category}</span>
                        <span>•</span>
                        <span>{req.roomNumber}</span>
                        {req.notes && (
                          <>
                            <span>•</span>
                            <span className="truncate max-w-[200px] text-[#0F1B1A]/50">{req.notes}</span>
                          </>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 self-start sm:self-center">
                    <span
                      className={`text-[10px] uppercase font-bold tracking-wider px-2.5 py-1 rounded-full border ${
                        req.status === "Resolved"
                          ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                          : req.status === "In Progress"
                          ? "bg-sky-50 text-sky-700 border-sky-200"
                          : "bg-amber-50 text-amber-700 border-amber-200"
                      }`}
                    >
                      {req.status}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* ─────────────────────────────────────────────────────────────
            6. STAY REVIEWS (Submitted reviews & Edit/Delete)
        ───────────────────────────────────────────────────────────── */}
        <section
          id="stay-reviews-card"
          className="p-6 rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm"
        >
          <div className="flex items-center justify-between mb-4">
            <div>
              <h4
                className="font-serif text-base sm:text-lg text-[#0F1B1A] font-semibold tracking-tight"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                Stay Reviews &amp; Testimonials
              </h4>
              <p className="text-xs text-[#0F1B1A]/70">Your submitted reviews for past completed reservations.</p>
            </div>
            <Star className="w-5 h-5 text-[#C4622D]" />
          </div>

          {isReviewsLoading ? (
            <div className="h-24 bg-[#F7F6F2] rounded-xl animate-pulse" />
          ) : reviews.length === 0 ? (
            <p className="text-xs text-[#0F1B1A]/50 italic py-3 text-center">No reviews submitted yet.</p>
          ) : (
            <div className="flex flex-col gap-3">
              {reviews.map((rev) => (
                <div
                  key={rev.id}
                  className="p-4 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/8 flex flex-col gap-2.5"
                >
                  <div className="flex items-center justify-between">
                    <div>
                      <span className="text-xs font-semibold text-[#0F1B1A]">{rev.roomTypeName}</span>
                      <span className="text-[11px] text-[#0F1B1A]/60 ml-2">({rev.stayDate})</span>
                    </div>

                    {/* Star Rating Display */}
                    <div className="flex items-center gap-1">
                      {Array.from({ length: 5 }).map((_, i) => (
                        <Star
                          key={i}
                          className={`w-3.5 h-3.5 ${
                            i < rev.rating ? "text-[#E07A3E] fill-[#E07A3E]" : "text-gray-300"
                          }`}
                        />
                      ))}
                    </div>
                  </div>

                  <p className="font-serif italic text-xs sm:text-sm text-[#0F1B1A]/85 leading-relaxed">
                    &ldquo;{rev.text}&rdquo;
                  </p>

                  <div className="flex items-center justify-end gap-3 pt-1 text-xs">
                    <button
                      type="button"
                      onClick={() => {
                        setEditingReview(rev);
                        setReviewRating(rev.rating);
                        setReviewText(rev.text);
                      }}
                      className="text-[#0F1B1A]/60 hover:text-[#C4622D] flex items-center gap-1 cursor-pointer transition-colors"
                    >
                      <Edit2 className="w-3 h-3" />
                      <span>Edit</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => deleteReviewMutation.mutate({ guestId, reviewId: rev.id })}
                      className="text-[#0F1B1A]/60 hover:text-red-500 flex items-center gap-1 cursor-pointer transition-colors"
                    >
                      <Trash2 className="w-3 h-3" />
                      <span>Delete</span>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Edit Review Modal Dialog */}
          {editingReview && (
            <div className="mt-4 p-4 rounded-xl bg-[#F7F6F2] border border-[#C4622D]/40 animate-in fade-in duration-150">
              <span className="text-xs font-semibold text-[#0F1B1A] block mb-2">Edit Your Review</span>

              {reviewError && <p className="text-xs text-red-500 mb-2">{reviewError}</p>}

              <div className="flex items-center gap-1 mb-3">
                {[1, 2, 3, 4, 5].map((star) => (
                  <button
                    key={star}
                    type="button"
                    onClick={() => setReviewRating(star)}
                    className="p-1 cursor-pointer"
                  >
                    <Star
                      className={`w-4 h-4 ${
                        star <= reviewRating ? "text-[#E07A3E] fill-[#E07A3E]" : "text-gray-300"
                      }`}
                    />
                  </button>
                ))}
              </div>

              <textarea
                rows={3}
                value={reviewText}
                onChange={(e) => setReviewText(e.target.value)}
                className="w-full p-3 rounded-xl bg-white border border-[#0F1B1A]/15 text-[#0F1B1A] text-xs focus:outline-none focus:border-[#C4622D]"
              />

              <div className="flex justify-end gap-2 mt-3">
                <button
                  type="button"
                  onClick={() => setEditingReview(null)}
                  className="px-3 py-1.5 rounded-lg text-xs text-[#0F1B1A]/60 hover:text-[#0F1B1A]"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleSaveReview}
                  className="px-4 py-1.5 rounded-lg bg-[#C4622D] text-white text-xs font-medium cursor-pointer shadow-sm hover:bg-[#E07A3E]"
                >
                  Save Review
                </button>
              </div>
            </div>
          )}
        </section>

        {/* ─────────────────────────────────────────────────────────────
            7. NOTIFICATION SETTINGS (Toggle List)
        ───────────────────────────────────────────────────────────── */}
        <section
          id="notification-settings-card"
          className="p-6 rounded-2xl border border-[#0F1B1A]/8 bg-white shadow-sm"
        >
          <div className="flex items-center justify-between mb-4">
            <div>
              <h4
                className="font-serif text-base sm:text-lg text-[#0F1B1A] font-semibold tracking-tight"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                Notification Preferences
              </h4>
              <p className="text-xs text-[#0F1B1A]/70">Choose alerts and communication channels for your sanctuary stays.</p>
            </div>
            <Bell className="w-5 h-5 text-[#C4622D]" />
          </div>

          <div className="flex flex-col divide-y divide-[#0F1B1A]/8">
            {/* Booking Updates */}
            <div className="py-3.5 flex items-center justify-between">
              <div>
                <span className="text-xs sm:text-sm font-medium text-[#0F1B1A] block">Booking &amp; Stay Updates</span>
                <span className="text-[11px] text-[#0F1B1A]/60">Instant check-in alerts, door code dispatches, and invoices.</span>
              </div>
              <input
                type="checkbox"
                checked={notifications?.bookingUpdates ?? true}
                onChange={(e) =>
                  updateNotifMutation.mutate({
                    guestId,
                    data: {
                      bookingUpdates: e.target.checked,
                      serviceRequestUpdates: notifications?.serviceRequestUpdates ?? true,
                      promotions: notifications?.promotions ?? false,
                    },
                  })
                }
                className="w-4 h-4 rounded bg-white border-[#0F1B1A]/20 text-[#C4622D] accent-[#C4622D] cursor-pointer"
              />
            </div>

            {/* Service Requests */}
            <div className="py-3.5 flex items-center justify-between">
              <div>
                <span className="text-xs sm:text-sm font-medium text-[#0F1B1A] block">Service Request Dispatch</span>
                <span className="text-[11px] text-[#0F1B1A]/60">Real-time status on housekeeping and concierge tasks.</span>
              </div>
              <input
                type="checkbox"
                checked={notifications?.serviceRequestUpdates ?? true}
                onChange={(e) =>
                  updateNotifMutation.mutate({
                    guestId,
                    data: {
                      bookingUpdates: notifications?.bookingUpdates ?? true,
                      serviceRequestUpdates: e.target.checked,
                      promotions: notifications?.promotions ?? false,
                    },
                  })
                }
                className="w-4 h-4 rounded bg-white border-[#0F1B1A]/20 text-[#C4622D] accent-[#C4622D] cursor-pointer"
              />
            </div>

            {/* Promotions */}
            <div className="py-3.5 flex items-center justify-between">
              <div>
                <span className="text-xs sm:text-sm font-medium text-[#0F1B1A] block">Exclusive Sanctuary Privileges</span>
                <span className="text-[11px] text-[#0F1B1A]/60">Seasonal retreat offers and member-only culinary tastings.</span>
              </div>
              <input
                type="checkbox"
                checked={notifications?.promotions ?? false}
                onChange={(e) =>
                  updateNotifMutation.mutate({
                    guestId,
                    data: {
                      bookingUpdates: notifications?.bookingUpdates ?? true,
                      serviceRequestUpdates: notifications?.serviceRequestUpdates ?? true,
                      promotions: e.target.checked,
                    },
                  })
                }
                className="w-4 h-4 rounded bg-white border-[#0F1B1A]/20 text-[#C4622D] accent-[#C4622D] cursor-pointer"
              />
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}
