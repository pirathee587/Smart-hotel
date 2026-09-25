"use client";

import { getStaffHome } from "@/lib/staffNavigation";
import Link from "next/link";
import { useRouter,useSearchParams } from "next/navigation";
import React,{ useEffect,useId,useState } from "react";
import { useAuthStore } from "../store/useAuthStore";

export default function StaffLoginView() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { loginEmployee, isLoading, isAuthenticated, user, mustResetPassword, initialize } = useAuthStore();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(true);
  const [formError, setFormError] = useState<string | null>(null);
  const [loginSuccess, setLoginSuccess] = useState(false);

  const emailId = useId();
  const passwordId = useId();
  const rememberId = useId();

  useEffect(() => {
    initialize();
  }, [initialize]);

  // If already authenticated as employee, redirect to reset password or target
  useEffect(() => {
    if (isAuthenticated && user?.userType === "employee") {
      if (mustResetPassword) {
        router.push("/portal/force-reset-password");
        return;
      }
      const redirect = searchParams.get("redirect") || getStaffHome(user);
      router.push(redirect);
    }
  }, [isAuthenticated, user, mustResetPassword, router, searchParams]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!email.trim() || !password) {
      setFormError("Please enter both work email and password.");
      return;
    }

    const res = await loginEmployee(email.trim(), password);

    if (res.success) {
      if (res.mustResetPassword) {
        router.push("/portal/force-reset-password");
        return;
      }
      setLoginSuccess(true);
      const loggedInUser = useAuthStore.getState().user;
      const redirect = searchParams.get("redirect") || getStaffHome(loggedInUser);
      setTimeout(() => {
        router.push(redirect);
      }, 900);
    } else {
      setFormError(res.error || "Invalid staff credentials. Please check your details and try again.");
    }
  };

  const handleQuickFill = (demoEmail: string, demoPass: string) => {
    setEmail(demoEmail);
    setPassword(demoPass);
    setFormError(null);
  };

  return (
    <div className="relative min-h-screen w-full flex flex-col lg:flex-row bg-[#0F1B1A] text-[#E7EFEC] font-sans antialiased selection:bg-[#C4622D] selection:text-white overflow-x-hidden">
      {/* ── LEFT HERO / IMAGE PANEL (Exact match with project theme) ── */}
      <div className="relative w-full lg:w-[48%] xl:w-[50%] min-h-[340px] sm:min-h-[420px] lg:min-h-screen flex flex-col justify-between p-6 sm:p-8 lg:p-12 overflow-hidden shrink-0">
        {/* Background Image: High-res Sanctuary / Slowhouse */}
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: "url('/images/slowhouse-hero.jpg')",
            backgroundPosition: "center 45%",
          }}
          aria-hidden="true"
        />

        {/* Cinematic Multi-stop Dark Gradient Overlay */}
        <div
          className="absolute inset-0 bg-gradient-to-b from-[#0F1B1A]/35 via-[#0F1B1A]/50 to-[#0F1B1A]/90 z-0 pointer-events-none"
          aria-hidden="true"
        />

        {/* Top Row: Wordmark + Rating Badge */}
        <div className="relative z-10 flex items-center justify-between w-full">
          <Link
            href="/"
            className="group inline-flex items-center gap-1.5 text-xl lg:text-2xl tracking-tight focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C4622D] rounded-lg"
            aria-label="SmartHotel Home"
          >
            <span className="font-semibold text-[#E7EFEC]">Smart</span>
            <span
              className="font-serif italic font-normal text-[#E07A3E] transition-colors group-hover:text-[#E7EFEC]"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Hotel
            </span>
          </Link>

          {/* Rating Badge */}
          <div className="flex items-center gap-1.5 sm:gap-2 px-3.5 py-1.5 rounded-full bg-[#16302C]/75 backdrop-blur-md border border-[rgba(231,239,236,0.15)] text-xs text-[#E7EFEC] shadow-sm">
            <span className="font-semibold text-[#E7EFEC]">4.98</span>
            <span className="text-[#9BAFA9]">·</span>
            <span className="text-[#9BAFA9] hidden sm:inline">300+ verified stays</span>
            <span className="text-[#9BAFA9] sm:hidden">Maskeliya</span>
          </div>
        </div>

        {/* Bottom Row: Pull-quote & Location (Visible on md+) */}
        <div className="relative z-10 hidden md:flex flex-col gap-2.5 max-w-[420px] mt-auto">
          <p
            className="font-serif italic text-base lg:text-lg leading-relaxed text-[#E7EFEC]/95 tracking-wide"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            &ldquo;The hearth was already lit when we opened the door. It felt like the house had been expecting us for weeks.&rdquo;
          </p>
          <div className="flex items-center justify-between text-xs text-[#9BAFA9]">
            <span>— Ingrid H., Condé Nast Traveler</span>
            <span className="text-[#7C9188]">Maskeliya, Sri Lanka</span>
          </div>
        </div>
      </div>

      {/* ── RIGHT FORM PANEL ── */}
      <div className="relative w-full flex-1 flex flex-col items-center justify-center px-4 py-8 sm:px-8 lg:px-12 bg-[#0F1B1A] z-10">
        <div className="w-full max-w-[470px] mx-auto flex flex-col gap-5 sm:gap-6">
          {/* Back to Home Navigation Button */}
          <div className="flex items-center justify-between w-full">
            <Link
              href="/"
              className="group inline-flex items-center gap-2 text-xs font-medium text-[#9BAFA9] hover:text-[#E7EFEC] transition-all duration-200 py-1.5 px-3 rounded-full bg-[#16302C]/60 hover:bg-[#16302C]/90 border border-[rgba(231,239,236,0.12)] hover:border-[#E07A3E]/40 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C4622D] shadow-sm cursor-pointer"
            >
              <svg
                className="w-3.5 h-3.5 text-[#E07A3E] transition-transform duration-200 group-hover:-translate-x-1"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth="2.5"
                aria-hidden="true"
              >
                <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
              </svg>
              <span>Back to home</span>
            </Link>
          </div>

          {/* Header */}
          <div className="flex flex-col gap-1.5 text-left">
            <h1
              className="font-serif text-2xl sm:text-3xl lg:text-[32px] leading-tight font-normal text-[#E7EFEC] tracking-tight"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Staff Sign In
            </h1>
            <p className="text-xs sm:text-sm text-[#9BAFA9] leading-relaxed">
              Sign in with your work email and password to access hotel operations.
            </p>
          </div>

          {/* Frosted Glass Card Shell */}
          <div
            id="auth-form-container"
            className="w-full p-6 sm:p-7 rounded-2xl border border-[rgba(231,239,236,0.15)] shadow-2xl backdrop-blur-[16px]"
            style={{ backgroundColor: "rgba(22, 48, 44, 0.42)" }}
          >
            {/* Inline Error Banner */}
            {formError && (
              <div
                role="alert"
                className="error-banner mb-4 p-3.5 rounded-xl bg-red-950/50 border border-red-500/35 text-red-200 text-xs sm:text-sm flex items-start gap-2.5 animate-in fade-in slide-in-from-top-1 duration-200"
              >
                <svg
                  className="w-4 h-4 sm:w-5 sm:h-5 text-red-400 shrink-0 mt-0.5"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth="2"
                  aria-hidden="true"
                >
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
                <div className="flex-1 leading-snug">{formError}</div>
              </div>
            )}

            {/* Inline Success Message */}
            {loginSuccess && (
              <div
                role="status"
                className="mb-4 p-3.5 rounded-xl bg-[#2F5C52]/50 border border-[#2F5C52]/70 text-[#E7EFEC] text-xs sm:text-sm flex items-center gap-2.5 animate-in fade-in slide-in-from-top-1 duration-200"
              >
                <svg
                  className="w-5 h-5 text-[#7C9188] shrink-0"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth="2"
                  aria-hidden="true"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                </svg>
                <span>Staff authenticated! Entering operations portal...</span>
              </div>
            )}

            <form onSubmit={handleSubmit} suppressHydrationWarning className="flex flex-col gap-4 sm:gap-4.5">
              {/* Email */}
              <div className="flex flex-col gap-1.5">
                <label htmlFor={emailId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                  Work email address
                </label>
                <input
                  id={emailId}
                  type="email"
                  name="email"
                  required
                  autoFocus
                  autoComplete="email"
                  placeholder="employee@smarthotel.com"
                  value={email}
                  onChange={(e) => {
                    setEmail(e.target.value);
                    if (formError) setFormError(null);
                  }}
                  suppressHydrationWarning
                  className="w-full h-11 sm:h-12 px-4 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-sm sm:text-[15px] transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                />
              </div>

              {/* Password */}
              <div className="flex flex-col gap-1.5">
                <div className="flex items-center justify-between">
                  <label htmlFor={passwordId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                    Password
                  </label>
                </div>
                <div className="relative">
                  <input
                    id={passwordId}
                    type={showPassword ? "text" : "password"}
                    name="password"
                    required
                    autoComplete="current-password"
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => {
                      setPassword(e.target.value);
                      if (formError) setFormError(null);
                    }}
                    suppressHydrationWarning
                    className="w-full h-11 sm:h-12 pl-4 pr-16 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-sm sm:text-[15px] transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    suppressHydrationWarning
                    className="absolute right-3.5 top-1/2 -translate-y-1/2 text-xs font-semibold text-[#9BAFA9] hover:text-[#E7EFEC] transition-colors px-2 py-1 rounded focus:outline-none focus-visible:ring-1 focus-visible:ring-[#C4622D]"
                    aria-label={showPassword ? "Hide password" : "Show password"}
                  >
                    {showPassword ? "Hide" : "Show"}
                  </button>
                </div>
              </div>

              {/* Remember Me Checkbox */}
              <div className="flex items-center gap-2.5 pt-0.5">
                <input
                  id={rememberId}
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  suppressHydrationWarning
                  className="w-4 h-4 rounded bg-[#0F1B1A] border-[rgba(231,239,236,0.25)] text-[#C4622D] focus:ring-[#C4622D] focus:ring-offset-0 accent-[#C4622D] cursor-pointer"
                />
                <label htmlFor={rememberId} className="text-xs sm:text-[13px] text-[#9BAFA9] select-none cursor-pointer">
                  Remember my sign-in on this device
                </label>
              </div>

              {/* Primary CTA */}
              <button
                type="submit"
                disabled={isLoading || loginSuccess}
                suppressHydrationWarning
                className="w-full h-12 sm:h-12.5 mt-1.5 rounded-xl bg-[#C4622D] hover:bg-[#E07A3E] text-[#E7EFEC] text-sm sm:text-base font-semibold tracking-wide transition-all duration-200 shadow-md hover:shadow-lg disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus-visible:ring-2 focus-visible:ring-[#E07A3E] cursor-pointer"
              >
                {isLoading ? (
                  <span className="inline-flex items-center justify-center gap-2">
                    <svg className="animate-spin h-5 w-5 text-[#E7EFEC]" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path
                        className="opacity-75"
                        fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                      />
                    </svg>
                    Signing in...
                  </span>
                ) : (
                  "Sign in"
                )}
              </button>
            </form>

            {/* Quick Demo Credentials Bar */}
            <div className="mt-5 pt-4 border-t border-[rgba(231,239,236,0.12)] flex flex-col gap-2">
              <span className="text-[11px] text-[#9BAFA9] font-medium uppercase tracking-wider text-center">
                Demo Credentials (One-Click Fill)
              </span>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => handleQuickFill("owner@smarthotel.com", "Owner@123!")}
                  suppressHydrationWarning
                  className="px-3 py-2 rounded-xl bg-[#0F1B1A]/80 hover:bg-[#16302C] border border-[rgba(231,239,236,0.14)] hover:border-[#E07A3E] text-[11px] text-[#E7EFEC] transition-all text-left flex flex-col cursor-pointer group"
                >
                  <span className="font-semibold text-[#E07A3E] group-hover:underline">Owner</span>
                  <span className="text-[10px] text-[#9BAFA9] truncate">owner@smarthotel.com</span>
                </button>
                <button
                  type="button"
                  onClick={() => handleQuickFill("admin@smarthotel.com", "Admin@123!")}
                  suppressHydrationWarning
                  className="px-3 py-2 rounded-xl bg-[#0F1B1A]/80 hover:bg-[#16302C] border border-[rgba(231,239,236,0.14)] hover:border-[#E07A3E] text-[11px] text-[#E7EFEC] transition-all text-left flex flex-col cursor-pointer group"
                >
                  <span className="font-semibold text-[#E07A3E] group-hover:underline">Administrator</span>
                  <span className="text-[10px] text-[#9BAFA9] truncate">admin@smarthotel.com</span>
                </button>
                <button
                  type="button"
                  onClick={() => handleQuickFill("manager@smarthotel.com", "Manager@123!")}
                  suppressHydrationWarning
                  className="px-3 py-2 rounded-xl bg-[#0F1B1A]/80 hover:bg-[#16302C] border border-[rgba(231,239,236,0.14)] hover:border-[#E07A3E] text-[11px] text-[#E7EFEC] transition-all text-left flex flex-col cursor-pointer group"
                >
                  <span className="font-semibold text-[#E07A3E] group-hover:underline">Manager</span>
                  <span className="text-[10px] text-[#9BAFA9] truncate">manager@smarthotel.com</span>
                </button>
                <button
                  type="button"
                  onClick={() => handleQuickFill("employee@smarthotel.com", "Employee@123!")}
                  suppressHydrationWarning
                  className="px-3 py-2 rounded-xl bg-[#0F1B1A]/80 hover:bg-[#16302C] border border-[rgba(231,239,236,0.14)] hover:border-[#E07A3E] text-[11px] text-[#E7EFEC] transition-all text-left flex flex-col cursor-pointer group"
                >
                  <span className="font-semibold text-[#E07A3E] group-hover:underline">Employee</span>
                  <span className="text-[10px] text-[#9BAFA9] truncate">employee@smarthotel.com</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
