"use client";

import api from "@/lib/axios";
import { AlertCircle,ArrowRight,Check,Eye,EyeOff,KeyRound,Loader2,Lock,ShieldCheck,X } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import React,{ useEffect,useState } from "react";
import { useAuthStore } from "../store/useAuthStore";

export default function ForceResetPasswordView() {
  const router = useRouter();
  const { user, isAuthenticated, mustResetPassword, updateAfterPasswordReset, initialize } = useAuthStore();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);

  useEffect(() => {
    initialize();
  }, [initialize]);

  // Auth guard:
  useEffect(() => {
    if (!isAuthenticated) {
      router.replace("/staff/login");
      return;
    }
    // If user already changed their password and doesn't need a reset, send to dashboard
    if (!mustResetPassword) {
      router.replace("/dashboard");
    }
  }, [isAuthenticated, mustResetPassword, router]);

  // Validation rules
  const rules = [
    { label: "At least 14 characters", valid: newPassword.length >= 14 },
    { label: "Uppercase letter (A-Z)", valid: /[A-Z]/.test(newPassword) },
    { label: "Lowercase letter (a-z)", valid: /[a-z]/.test(newPassword) },
    { label: "Numeric digit (0-9)", valid: /[0-9]/.test(newPassword) },
    { label: "Special symbol (!@#$%^&*…)", valid: /[^A-Za-z0-9]/.test(newPassword) },
  ];

  const allRulesPassed = rules.every((r) => r.valid);
  const passwordsMatch = newPassword.length > 0 && newPassword === confirmPassword;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!currentPassword) {
      setFormError("Please enter your current temporary password.");
      return;
    }

    if (!allRulesPassed) {
      setFormError("Please ensure your new password satisfies all security criteria below.");
      return;
    }

    if (!passwordsMatch) {
      setFormError("New password and confirmation do not match.");
      return;
    }

    setSubmitting(true);
    try {
      const response = await api.post("/api/v1/auth/change-password", {
        currentPassword,
        newPassword,
      });

      const data = response.data || {};
      const newToken = data.accessToken || data.token;

      // Update auth store to clear mustResetPassword flag and update token
      const needsProfile = Boolean(data.profileCompletionRequired);
      updateAfterPasswordReset(newToken, needsProfile, data.profileCompletionDeadlineUtc, data.refreshToken);

      setIsSuccess(true);
      setTimeout(() => {
        const role = String(useAuthStore.getState().user?.role ?? "");
        router.push(needsProfile ? "/portal/complete-profile" : role === "Owner" ? "/owner" : "/dashboard");
      }, 1500);
    } catch (requestError: unknown) {
      const err = requestError as { response?: { data?: { errors?: string[]; message?: string } }; message?: string };
      const resp = err.response?.data;
      let msg = "Failed to update password. Please verify your temporary password.";

      if (resp?.errors && Array.isArray(resp.errors) && resp.errors.length > 0) {
        msg = resp.errors.join(". ");
      } else if (resp?.message) {
        msg = resp.message;
      } else if (err?.message) {
        msg = err.message;
      }

      setFormError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="relative min-h-screen w-full flex items-center justify-center bg-[#0F1B1A] text-[#E7EFEC] font-sans antialiased selection:bg-[#C4622D] selection:text-white p-4 sm:p-6 lg:p-8">
      {/* Cinematic subtle background glowing blur elements */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[520px] h-[320px] bg-[#C4622D]/10 blur-[130px] rounded-full pointer-events-none" />
      <div className="absolute bottom-10 right-10 w-[380px] h-[280px] bg-[#2F5C52]/20 blur-[110px] rounded-full pointer-events-none" />

      <div className="relative w-full max-w-lg z-10">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-2xl tracking-tight focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C4622D] rounded-lg"
          >
            <span className="font-semibold text-[#E7EFEC]">Smart</span>
            <span
              className="font-serif italic font-normal text-[#E07A3E]"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Hotel
            </span>
          </Link>
          <div className="flex items-center justify-center gap-1.5 mt-2">
            <span className="w-1.5 h-1.5 rounded-full bg-[#C4622D]" />
            <span className="text-[11px] font-semibold tracking-widest text-[#8A9E99] uppercase">
              Staff Security Onboarding
            </span>
          </div>
        </div>

        {/* Card */}
        <div className="bg-[#142322]/85 backdrop-blur-xl border border-[#233835] rounded-3xl p-6 sm:p-9 shadow-2xl shadow-black/60">
          {isSuccess ? (
            <div className="text-center py-8 space-y-4">
              <div className="w-16 h-16 rounded-2xl bg-[#2F5C52]/30 border border-[#2F5C52] text-[#4ADE80] flex items-center justify-center mx-auto animate-bounce">
                <ShieldCheck className="w-8 h-8" />
              </div>
              <h2
                className="text-2xl font-semibold text-[#E7EFEC]"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                Password Updated!
              </h2>
              <p className="text-sm text-[#A2B3AF] max-w-xs mx-auto">
                Your password is set. Complete and upload all required profile details within two days, or your credentials will be disabled.
              </p>
              <div className="flex items-center justify-center gap-2 pt-4 text-xs text-[#E07A3E] font-medium">
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>Redirecting to profile setup…</span>
              </div>
            </div>
          ) : (
            <>
              {/* Card Header */}
              <div className="mb-6">
                <div className="flex items-center gap-2.5 mb-2">
                  <div className="w-9 h-9 rounded-xl bg-[#C4622D]/15 border border-[#C4622D]/30 flex items-center justify-center text-[#E07A3E]">
                    <KeyRound className="w-4 h-4" />
                  </div>
                  <div>
                    <h1
                      className="text-xl sm:text-2xl font-bold text-[#E7EFEC]"
                      style={{ fontFamily: "var(--font-fraunces), serif" }}
                    >
                      Set Your Permanent Password
                    </h1>
                  </div>
                </div>
                <p className="text-xs sm:text-sm text-[#8FA5A0] leading-relaxed">
                  Welcome to the SmartHotel team
                  {user?.name ? `, ${user.name}` : ""}. Because this is your first login, please create a new secure password to replace your temporary credentials.
                </p>
              </div>

              {/* Error Message */}
              {formError && (
                <div className="mb-5 flex items-start gap-2.5 p-3.5 rounded-xl bg-[#C4622D]/10 border border-[#C4622D]/30 text-xs text-[#F87171]">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span className="leading-snug">{formError}</span>
                </div>
              )}

              {/* Form */}
              <form onSubmit={handleSubmit} className="space-y-4">
                {/* Temporary Password */}
                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-[#A2B3AF] mb-1.5">
                    Current (Temporary) Password
                  </label>
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-[#5A706B]">
                      <Lock className="w-4 h-4" />
                    </div>
                    <input
                      type={showCurrent ? "text" : "password"}
                      required
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder="Enter the password emailed to you"
                      className="w-full pl-10 pr-10 py-2.5 bg-[#0B1514] border border-[#233835] focus:border-[#C4622D] rounded-xl text-sm text-[#E7EFEC] placeholder-[#4F635E] focus:outline-none focus:ring-1 focus:ring-[#C4622D] transition-colors font-mono"
                    />
                    <button
                      type="button"
                      onClick={() => setShowCurrent(!showCurrent)}
                      className="absolute inset-y-0 right-0 pr-3.5 flex items-center text-[#5A706B] hover:text-[#A2B3AF] transition-colors cursor-pointer"
                    >
                      {showCurrent ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                {/* New Password */}
                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-[#A2B3AF] mb-1.5">
                    New Password
                  </label>
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-[#5A706B]">
                      <KeyRound className="w-4 h-4" />
                    </div>
                    <input
                      type={showNew ? "text" : "password"}
                      required
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="Minimum 14 characters"
                      className="w-full pl-10 pr-10 py-2.5 bg-[#0B1514] border border-[#233835] focus:border-[#C4622D] rounded-xl text-sm text-[#E7EFEC] placeholder-[#4F635E] focus:outline-none focus:ring-1 focus:ring-[#C4622D] transition-colors"
                    />
                    <button
                      type="button"
                      onClick={() => setShowNew(!showNew)}
                      className="absolute inset-y-0 right-0 pr-3.5 flex items-center text-[#5A706B] hover:text-[#A2B3AF] transition-colors cursor-pointer"
                    >
                      {showNew ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                {/* Confirm New Password */}
                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-[#A2B3AF] mb-1.5">
                    Confirm New Password
                  </label>
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-[#5A706B]">
                      <KeyRound className="w-4 h-4" />
                    </div>
                    <input
                      type={showConfirm ? "text" : "password"}
                      required
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="Re-enter your new password"
                      className={`w-full pl-10 pr-10 py-2.5 bg-[#0B1514] border rounded-xl text-sm text-[#E7EFEC] placeholder-[#4F635E] focus:outline-none focus:ring-1 transition-colors ${
                        confirmPassword.length > 0 && !passwordsMatch
                          ? "border-[#EF4444] focus:border-[#EF4444] focus:ring-[#EF4444]"
                          : "border-[#233835] focus:border-[#C4622D] focus:ring-[#C4622D]"
                      }`}
                    />
                    <button
                      type="button"
                      onClick={() => setShowConfirm(!showConfirm)}
                      className="absolute inset-y-0 right-0 pr-3.5 flex items-center text-[#5A706B] hover:text-[#A2B3AF] transition-colors cursor-pointer"
                    >
                      {showConfirm ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                  {confirmPassword.length > 0 && !passwordsMatch && (
                    <p className="mt-1 text-[11px] text-[#EF4444]">Passwords do not match.</p>
                  )}
                </div>

                {/* Security Criteria Checklist */}
                <div className="p-3.5 rounded-2xl bg-[#0B1514]/70 border border-[#233835] space-y-1.5">
                  <p className="text-[11px] font-semibold uppercase tracking-wider text-[#8A9E99] mb-2">
                    Password Security Criteria:
                  </p>
                  {rules.map((rule, idx) => (
                    <div key={idx} className="flex items-center gap-2 text-xs">
                      <div
                        className={`w-4 h-4 rounded-full flex items-center justify-center shrink-0 transition-colors ${
                          rule.valid
                            ? "bg-[#2F5C52] text-white"
                            : "bg-[#1E2E2C] text-[#5A706B]"
                        }`}
                      >
                        {rule.valid ? <Check className="w-2.5 h-2.5" /> : <X className="w-2.5 h-2.5" />}
                      </div>
                      <span className={rule.valid ? "text-[#D0DCD8]" : "text-[#6A817C]"}>
                        {rule.label}
                      </span>
                    </div>
                  ))}
                </div>

                {/* Submit button */}
                <button
                  type="submit"
                  disabled={submitting || !allRulesPassed || !passwordsMatch}
                  className="w-full mt-2 py-3 px-4 bg-gradient-to-r from-[#C4622D] to-[#E07A3E] hover:from-[#B35524] hover:to-[#D26F35] text-white font-semibold text-sm rounded-xl shadow-lg shadow-[#C4622D]/20 transition-all transform active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 cursor-pointer"
                >
                  {submitting ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" />
                      <span>Updating Password…</span>
                    </>
                  ) : (
                    <>
                      <span>Save & Continue to Dashboard</span>
                      <ArrowRight className="w-4 h-4" />
                    </>
                  )}
                </button>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
