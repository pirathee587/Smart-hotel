"use client";

import api from "@/lib/axios";
import Link from "next/link";
import { useRouter,useSearchParams } from "next/navigation";
import React,{ useEffect,useEffectEvent,useId,useMemo,useState } from "react";

type GooglePromptNotification = { isNotDisplayed?: () => boolean; isSkippedMoment?: () => boolean };
type GoogleIdentityApi = {
  initialize: (options: { client_id: string; callback: (response: { credential?: string }) => void | Promise<void> }) => void;
  prompt: (callback: (notification: GooglePromptNotification) => void) => void;
};
type GoogleWindow = Window & { google?: { accounts?: { id?: GoogleIdentityApi } } };
import { useAuthStore } from "../store/useAuthStore";

// Real 4-color Google "G" logo SVG
function GoogleIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4 sm:w-5 sm:h-5 shrink-0" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.27-4.74 3.27-8.1Z"
      />
      <path
        fill="#34A853"
        d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84A11 11 0 0 0 12 23Z"
      />
      <path
        fill="#FBBC05"
        d="M5.84 14.1a6.6 6.6 0 0 1 0-4.2V7.06H2.18a11 11 0 0 0 0 9.88l3.66-2.84Z"
      />
      <path
        fill="#EA4335"
        d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06L5.84 9.9C6.71 7.31 9.14 5.38 12 5.38Z"
      />
    </svg>
  );
}

const COUNTRIES = [
  "Sri Lanka",
  "United States",
  "United Kingdom",
  "Australia",
  "Germany",
  "France",
  "Canada",
  "Singapore",
  "India",
  "Japan",
  "Switzerland",
  "Netherlands",
  "United Arab Emirates",
  "Other",
];

interface AuthViewProps {
  mode?: "login" | "register";
}

export default function AuthView({ mode = "login" }: AuthViewProps) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const {
    loginCustomer,
    loginWithGoogle,
    registerCustomer,
    isLoading,
    error,
    clearError,
    isAuthenticated,
    user,
    initialize,
  } = useAuthStore();

  // Mode: Toggle between Guest Sign In and Guest Create Account
  const [authMode, setAuthMode] = useState<"login" | "register">(() => {
    const m = searchParams.get("mode") || searchParams.get("tab");
    if (m === "register" || m === "signup") return "register";
    return mode;
  });
  const isLogin = authMode === "login";

  useEffect(() => {
    const m = searchParams.get("mode") || searchParams.get("tab");
    if (searchParams.get("role") === "staff" || searchParams.get("staff") === "true") {
      router.replace("/staff/login");
      return;
    }
    if (m === "register" || m === "signup") {
      queueMicrotask(() => setAuthMode("register"));
    } else if (m === "login" || m === "signin") {
      queueMicrotask(() => setAuthMode("login"));
    }
  }, [searchParams, router]);

  // Common Form States
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Signup-specific States
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [nationalId, setNationalId] = useState("");
  const [nationality, setNationality] = useState("");
  const [agreedToTerms, setAgreedToTerms] = useState(false);
  const [signupSuccess, setSignupSuccess] = useState(false);
  const [verificationNotice, setVerificationNotice] = useState<string | null>(null);
  const [bookingReference, setBookingReference] = useState("");
  const bookingRefId = useId();

  // Generated unique IDs for accessibility
  const emailId = useId();
  const passwordId = useId();
  const confirmPasswordId = useId();
  const firstNameId = useId();
  const lastNameId = useId();
  const phoneId = useId();
  const nationalIdField = useId();
  const nationalityId = useId();
  const termsId = useId();
  const rememberId = useId();

  // Extract ?ref= and ?email= from query parameters
  useEffect(() => {
    const refParam = searchParams.get("ref");
    const emailParam = searchParams.get("email");
    if (refParam) {
      queueMicrotask(() => setBookingReference(refParam.toUpperCase().trim()));
    }
    if (emailParam) {
      queueMicrotask(() => setEmail(emailParam.trim()));
    }
  }, [searchParams]);

  // Initialize store and redirect if already authenticated
  useEffect(() => {
    initialize();
  }, [initialize]);

  useEffect(() => {
    if (isAuthenticated && user) {
      router.push("/rooms");
    }
  }, [isAuthenticated, user, router]);

  // Clear errors when switching modes
  useEffect(() => {
    queueMicrotask(() => {
      clearError();
      setFormError(null);
    });
  }, [authMode, clearError]);

  // Password strength calculation
  const passwordStrength = useMemo(() => {
    if (!password) return { score: 0, text: "" };

    let score = 0;
    if (password.length >= 8) score += 1;
    if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score += 1;
    if (/\d/.test(password)) score += 1;
    if (/[^A-Za-z0-9]/.test(password)) score += 1;

    let text = "";
    if (score === 0) text = "8+ characters with mixed case, number & symbol";
    else if (score === 1) text = "Weak — add mixed case, numbers and symbols";
    else if (score === 2) text = "Fair — add numbers and symbols";
    else if (score === 3) text = "Good — add special characters";
    else if (score === 4) text = "Strong password";

    return { score, text };
  }, [password]);

  // Confirm password validation
  const passwordsMatch = confirmPassword.length > 0 && password === confirmPassword;
  const passwordsMismatch = confirmPassword.length > 0 && password !== confirmPassword;

  // Google OAuth State & Handler
  const [googleLoading, setGoogleLoading] = useState(false);

  const handleAuthResult = (result: { success: boolean; role?: string; error?: string }) => {
    if (result.success) {
      router.push("/rooms");
    } else {
      setFormError(result.error || "Google sign-in failed. Please try again or sign in with your email.");
    }
  };
  const handleGoogleResult = useEffectEvent(handleAuthResult);

  useEffect(() => {
    const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
    if (!clientId) return;

    const scriptId = "google-gis-sdk";
    if (document.getElementById(scriptId)) return;

    const script = document.createElement("script");
    script.id = scriptId;
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = () => {
      const google = (window as GoogleWindow).google;
      if (google?.accounts?.id) {
        google.accounts.id.initialize({
          client_id: clientId,
          callback: async (response: { credential?: string }) => {
            if (response?.credential) {
              setGoogleLoading(true);
              const result = await loginWithGoogle(response.credential);
              setGoogleLoading(false);
              handleGoogleResult(result);
            }
          },
        });
      }
    };
    document.body.appendChild(script);
  }, [loginWithGoogle]);

  const handleGoogleAuth = async () => {
    clearError();
    setFormError(null);
    const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
    const google = typeof window !== "undefined" ? (window as GoogleWindow).google : null;

    if (clientId && google?.accounts?.id) {
      setGoogleLoading(true);
      google.accounts.id.initialize({
        client_id: clientId,
        callback: async (response: { credential?: string }) => {
          if (response?.credential) {
            const result = await loginWithGoogle(response.credential);
            setGoogleLoading(false);
            handleAuthResult(result);
          } else {
            setGoogleLoading(false);
          }
        },
      });
      google.accounts.id.prompt((notification: GooglePromptNotification) => {
        if (notification?.isNotDisplayed?.() || notification?.isSkippedMoment?.()) {
          setGoogleLoading(false);
        }
      });
      return;
    }

    setFormError("Google Sign-In is not configured for this environment.");
  };

  // Submit Guest Login
  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    clearError();

    if (!email.trim() || !password) {
      setFormError("Please enter both email and password.");
      return;
    }

    const result = await loginCustomer(email.trim(), password);
    if (result.success) {
      router.push("/rooms");
    } else {
      setFormError(result.error || "Invalid email or password. Please try again.");
    }
  };

  // Submit Guest Registration
  const handleRegisterSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    clearError();

    if (!firstName.trim() || !lastName.trim() || !email.trim() || !phone.trim() || !password) {
      setFormError("Please fill in all required fields.");
      return;
    }

    if (password.length < 8) {
      setFormError("Password must be at least 8 characters long.");
      return;
    }

    if (password !== confirmPassword) {
      setFormError("Passwords do not match.");
      return;
    }

    if (!agreedToTerms) {
      setFormError("Please accept the Terms of Stay and Privacy Policy.");
      return;
    }

    const result = await registerCustomer({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      email: email.trim(),
      phone: phone.trim(),
      password,
      nationalId: nationalId.trim() || undefined,
      nationality: nationality.trim() || undefined,
    });

    if (result.success) {
      const needsVerification =
        result.message?.toLowerCase().includes("check your email") ||
        result.message?.toLowerCase().includes("verify your account");

      if (needsVerification) {
        setVerificationNotice(
          result.message || "Registration successful! Please check your email to verify your account."
        );
        setSignupSuccess(false);
      } else {
        setSignupSuccess(true);
        const loginRes = await loginCustomer(email.trim(), password);
        if (loginRes.success) {
          if (bookingReference.trim()) {
            try {
              await api.post("/api/bookings/claim", {
                bookingReference: bookingReference.trim(),
                email: email.trim(),
              });
            } catch (claimErr) {
              console.warn("Auto-claim notice:", claimErr);
            }
          }
          setTimeout(() => {
            router.push("/rooms");
          }, 1200);
        } else {
          setFormError(loginRes.error || "Registration complete. Please sign in.");
          setTimeout(() => {
            setAuthMode("login");
            setSignupSuccess(false);
          }, 1500);
        }
      }
    } else {
      setFormError(result.error || "Registration failed. Please try again.");
    }
  };


  const activeError = formError || error;

  return (
    <div className="min-h-screen w-full bg-[#0F1B1A] text-[#E7EFEC] font-sans antialiased flex flex-col md:grid md:grid-cols-2 lg:grid-cols-[1.08fr_1fr] relative overflow-x-hidden selection:bg-[#C4622D]/30 selection:text-[#E7EFEC]">
      {/* ── LEFT VISUAL PANEL ── */}
      <div className="relative w-full h-[220px] sm:h-[260px] md:h-screen md:min-h-screen md:sticky md:top-0 overflow-hidden flex flex-col justify-between p-6 sm:p-8 lg:p-12 z-0 shrink-0">
        {/* Cross-fading background photos */}
        <div
          className={`absolute inset-0 bg-cover bg-center transition-opacity duration-700 ease-in-out motion-reduce:transition-none ${
            isLogin ? "opacity-100 z-0" : "opacity-0 -z-10"
          }`}
          style={{
            backgroundImage: "url('/images/slowhouse-hero.jpg')",
            backgroundPosition: "center 45%",
          }}
          aria-hidden="true"
        />
        <div
          className={`absolute inset-0 bg-cover bg-center transition-opacity duration-700 ease-in-out motion-reduce:transition-none ${
            !isLogin ? "opacity-100 z-0" : "opacity-0 -z-10"
          }`}
          style={{
            backgroundImage: "url('/images/hero-view.jpg')",
            backgroundPosition: "center 40%",
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
              {isLogin ? "Guest Sign In" : "Create Guest Account"}
            </h1>
            <p className="text-xs sm:text-sm text-[#9BAFA9] leading-relaxed">
              {isLogin
                ? "Sign in with your email or Google to manage your sanctuary reservation."
                : "Register for exclusive member rates, faster checkout, and concierge privileges."}
            </p>
          </div>


          {/* Frosted Glass Card Shell */}
          <div
            id="auth-form-container"
            className="w-full p-6 sm:p-7 rounded-2xl border border-[rgba(231,239,236,0.15)] shadow-2xl backdrop-blur-[16px]"
            style={{ backgroundColor: "rgba(22, 48, 44, 0.42)" }}
          >
            {/* Inline Error Banner */}
            {activeError && (
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
                <div className="flex-1 leading-snug">
                  <div>{activeError}</div>
                  {activeError.toLowerCase().includes("verify") && (
                    <div className="mt-2.5 pt-2 border-t border-red-500/20 flex flex-wrap items-center gap-3">
                      <Link
                        href={`/verify-email?email=${encodeURIComponent(email)}`}
                        className="text-[#E07A3E] font-medium hover:underline text-xs inline-flex items-center gap-1"
                      >
                        Enter verification code / link &rarr;
                      </Link>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Inline Email Verification Notice */}
            {verificationNotice && (
              <div
                role="status"
                className="mb-4 p-4 rounded-xl bg-[#16302C] border border-[#C4622D]/40 text-[#E7EFEC] text-xs sm:text-sm flex flex-col gap-3 animate-in fade-in slide-in-from-top-1 duration-200 shadow-lg"
              >
                <div className="flex items-start gap-2.5">
                  <svg
                    className="w-5 h-5 text-[#E07A3E] shrink-0 mt-0.5"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                    strokeWidth="2"
                    aria-hidden="true"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                    />
                  </svg>
                  <div className="flex-1">
                    <p className="font-medium text-white">{verificationNotice}</p>
                    <p className="text-xs text-[#9BAFA9] mt-1">
                      We sent a verification link to <strong className="text-white">{email}</strong>. Please check your inbox or spam folder.
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-3 pt-2 border-t border-white/10">
                  <Link
                    href={`/verify-email?email=${encodeURIComponent(email)}`}
                    className="px-3.5 py-1.5 rounded-lg bg-[#C4622D] hover:bg-[#A84F20] text-white text-xs font-medium transition-colors"
                  >
                    Verify Email Now
                  </Link>
                  <button
                    type="button"
                    suppressHydrationWarning
                    onClick={() => {
                      setVerificationNotice(null);
                      setAuthMode("login");
                    }}
                    className="text-xs text-[#9BAFA9] hover:text-white underline"
                  >
                    Go to Sign In
                  </button>
                </div>
              </div>
            )}

            {/* Inline Signup Success Message */}
            {signupSuccess && (
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
                <span>Account created! Logging in and redirecting...</span>
              </div>
            )}

            {/* FORM: GUEST LOGIN */}
            {isLogin ? (
              <form id="login-form" onSubmit={handleLoginSubmit} suppressHydrationWarning className="flex flex-col gap-4 sm:gap-4.5">
                {/* Email */}
                <div className="flex flex-col gap-1.5">
                  <label htmlFor={emailId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                    Email address
                  </label>
                  <input
                    id={emailId}
                    type="email"
                    name="email"
                    required
                    autoComplete="email"
                    placeholder="guest@example.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
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
                      onChange={(e) => setPassword(e.target.value)}
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
                  disabled={isLoading || signupSuccess}
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

                {/* Labeled Divider & Google OAuth */}
                <div className="relative flex items-center justify-center my-1">
                  <div className="absolute inset-0 flex items-center">
                    <div className="w-full border-t border-[rgba(231,239,236,0.14)]" />
                  </div>
                  <div className="relative px-3 bg-[#16302C]/60 backdrop-blur-sm text-[11px] uppercase tracking-wider text-[#9BAFA9] font-medium rounded-full">
                    or
                  </div>
                </div>

                <button
                  type="button"
                  onClick={handleGoogleAuth}
                  disabled={isLoading || googleLoading}
                  suppressHydrationWarning
                  className="w-full h-11 sm:h-12 px-4 rounded-xl bg-transparent hover:bg-white/[0.04] border border-[rgba(231,239,236,0.18)] hover:border-[rgba(231,239,236,0.35)] text-[#E7EFEC] text-xs sm:text-sm font-medium inline-flex items-center justify-center gap-3 transition-all duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C4622D] cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {googleLoading ? (
                    <span className="inline-flex items-center gap-2">
                      <span className="w-4 h-4 border-2 border-white/20 border-t-white rounded-full animate-spin" />
                      Connecting with Google...
                    </span>
                  ) : (
                    <>
                      <GoogleIcon />
                      Continue with Google
                    </>
                  )}
                </button>
              </form>
            ) : (
              /* FORM: GUEST REGISTRATION */
              <form onSubmit={handleRegisterSubmit} suppressHydrationWarning className="flex flex-col gap-3.5 sm:gap-4">
                {/* First Name & Last Name (2 columns) */}
                <div className="grid grid-cols-2 gap-3 sm:gap-3.5">
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor={firstNameId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                      First name *
                    </label>
                    <input
                      id={firstNameId}
                      type="text"
                      required
                      autoComplete="given-name"
                      placeholder="Jane"
                      value={firstName}
                      onChange={(e) => setFirstName(e.target.value)}
                      suppressHydrationWarning
                      className="w-full h-11 sm:h-11.5 px-3.5 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor={lastNameId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                      Last name *
                    </label>
                    <input
                      id={lastNameId}
                      type="text"
                      required
                      autoComplete="family-name"
                      placeholder="Doe"
                      value={lastName}
                      onChange={(e) => setLastName(e.target.value)}
                      suppressHydrationWarning
                      className="w-full h-11 sm:h-11.5 px-3.5 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                    />
                  </div>
                </div>

                {/* Email Address */}
                <div className="flex flex-col gap-1.5">
                  <label htmlFor={emailId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                    Email address *
                  </label>
                  <input
                    id={emailId}
                    type="email"
                    required
                    autoComplete="email"
                    placeholder="jane.doe@example.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    suppressHydrationWarning
                    className="w-full h-11 sm:h-11.5 px-3.5 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                  />
                </div>

                {/* Phone Number */}
                <div className="flex flex-col gap-1.5">
                  <label htmlFor={phoneId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                    Phone number *
                  </label>
                  <input
                    id={phoneId}
                    type="tel"
                    required
                    autoComplete="tel"
                    placeholder="+1 (555) 000-0000"
                    value={phone}
                    onChange={(e) => setPhone(e.target.value)}
                    suppressHydrationWarning
                    className="w-full h-11 sm:h-11.5 px-3.5 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                  />
                </div>

                {/* Password with Strength Meter */}
                <div className="flex flex-col gap-1.5">
                  <label htmlFor={passwordId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                    Password *
                  </label>
                  <div className="relative">
                    <input
                      id={passwordId}
                      type={showPassword ? "text" : "password"}
                      required
                      autoComplete="new-password"
                      placeholder="••••••••"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      suppressHydrationWarning
                      className="w-full h-11 sm:h-11.5 pl-3.5 pr-16 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
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

                  {/* 4-Segment Password Strength Meter */}
                  {password.length > 0 && (
                    <div className="pt-1.5 flex flex-col gap-1">
                      <div className="grid grid-cols-4 gap-1.5 h-1.5">
                        {[1, 2, 3, 4].map((seg) => {
                          let barColor = "bg-[rgba(231,239,236,0.14)]";
                          if (seg <= passwordStrength.score) {
                            if (passwordStrength.score === 1 || passwordStrength.score === 2) {
                              barColor = "bg-[#C08A3E]"; // Gold
                            } else if (passwordStrength.score === 3) {
                              barColor = "bg-[#C4622D]"; // Ember
                            } else {
                              barColor = "bg-[#2F5C52]"; // Pine
                            }
                          }
                          return (
                            <div
                              key={seg}
                              className={`h-full rounded-full transition-all duration-200 ${barColor}`}
                            />
                          );
                        })}
                      </div>
                      <span className="text-xs text-[#9BAFA9] leading-tight">
                        {passwordStrength.text}
                      </span>
                    </div>
                  )}
                </div>

                {/* Confirm Password with Match Indicator */}
                <div className="flex flex-col gap-1.5">
                  <label htmlFor={confirmPasswordId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC]">
                    Confirm password *
                  </label>
                  <div className="relative">
                    <input
                      id={confirmPasswordId}
                      type={showConfirmPassword ? "text" : "password"}
                      required
                      autoComplete="new-password"
                      placeholder="••••••••"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      suppressHydrationWarning
                      className="w-full h-11 sm:h-11.5 pl-3.5 pr-16 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                    />
                    <button
                      type="button"
                      onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                      suppressHydrationWarning
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 text-xs font-semibold text-[#9BAFA9] hover:text-[#E7EFEC] transition-colors px-2 py-1 rounded focus:outline-none focus-visible:ring-1 focus-visible:ring-[#C4622D]"
                      aria-label={showConfirmPassword ? "Hide password" : "Show password"}
                    >
                      {showConfirmPassword ? "Hide" : "Show"}
                    </button>
                  </div>
                  {/* Live Match Feedback */}
                  {confirmPassword.length > 0 && (
                    <span
                      className={`text-xs font-medium flex items-center gap-1.5 ${
                        passwordsMatch ? "text-[#7C9188]" : "text-[#C08A3E]"
                      }`}
                    >
                      {passwordsMatch ? "✓ Passwords match" : "• Passwords don't match yet"}
                    </span>
                  )}
                </div>

                {/* Optional National ID & Nationality (2 columns) */}
                <div className="grid grid-cols-2 gap-3 sm:gap-3.5">
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor={nationalIdField} className="text-xs font-medium text-[#9BAFA9]">
                      Passport / ID <span className="opacity-70">(optional)</span>
                    </label>
                    <input
                      id={nationalIdField}
                      type="text"
                      placeholder="N12345678"
                      value={nationalId}
                      onChange={(e) => setNationalId(e.target.value)}
                      suppressHydrationWarning
                      className="w-full h-11 sm:h-11.5 px-3.5 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor={nationalityId} className="text-xs font-medium text-[#9BAFA9]">
                      Nationality <span className="opacity-70">(optional)</span>
                    </label>
                    <div className="relative">
                      <select
                        id={nationalityId}
                        value={nationality}
                        onChange={(e) => setNationality(e.target.value)}
                        suppressHydrationWarning
                        className="w-full h-11 sm:h-11.5 px-3.5 pr-8 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] text-xs sm:text-sm transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30 appearance-none cursor-pointer"
                      >
                        <option value="" className="bg-[#0F1B1A] text-[#9BAFA9]">
                          Select nationality
                        </option>
                        {COUNTRIES.map((c) => (
                          <option key={c} value={c} className="bg-[#0F1B1A] text-[#E7EFEC]">
                            {c}
                          </option>
                        ))}
                      </select>
                      <div className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-[#9BAFA9]">
                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M19 9l-7 7-7-7" />
                        </svg>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Optional Booking Reference to Claim */}
                <div className="flex flex-col gap-1.5">
                  <label htmlFor={bookingRefId} className="text-xs sm:text-[13px] font-medium text-[#E7EFEC] flex items-center justify-between">
                    <span>Booking Reference <span className="text-[#9BAFA9] font-normal text-xs">(Optional)</span></span>
                    <span className="text-[11px] text-[#E07A3E] font-mono">e.g. TH-2026-XXXXXX</span>
                  </label>
                  <input
                    id={bookingRefId}
                    type="text"
                    placeholder="TH-2026-483920"
                    value={bookingReference}
                    onChange={(e) => setBookingReference(e.target.value.toUpperCase())}
                    suppressHydrationWarning
                    className="w-full h-11 sm:h-11.5 px-3.5 rounded-xl bg-[#0F1B1A]/75 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/50 text-xs sm:text-sm font-mono uppercase tracking-wider transition-all focus:outline-none focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/30"
                  />
                  <p className="text-[11px] text-[#9BAFA9] leading-tight">
                    Already have a reservation? Enter reference to link it to your new guest profile.
                  </p>
                </div>

                {/* Terms of Service Checkbox (Required) */}
                <div className="flex items-start gap-2.5 pt-1">
                  <input
                    id={termsId}
                    type="checkbox"
                    required
                    checked={agreedToTerms}
                    onChange={(e) => setAgreedToTerms(e.target.checked)}
                    suppressHydrationWarning
                    className="w-4 h-4 mt-0.5 rounded bg-[#0F1B1A] border-[rgba(231,239,236,0.25)] text-[#C4622D] focus:ring-[#C4622D] focus:ring-offset-0 accent-[#C4622D] cursor-pointer"
                  />
                  <label htmlFor={termsId} className="text-xs text-[#9BAFA9] leading-snug select-none cursor-pointer">
                    I agree to the Terms of Stay and Privacy Policy.
                  </label>
                </div>

                {/* Primary CTA */}
                <button
                  type="submit"
                  disabled={isLoading || signupSuccess || passwordsMismatch}
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
                      Creating account...
                    </span>
                  ) : (
                    "Create guest account"
                  )}
                </button>

                {/* Labeled Divider */}
                <div className="relative flex items-center justify-center my-1">
                  <div className="absolute inset-0 flex items-center">
                    <div className="w-full border-t border-[rgba(231,239,236,0.14)]" />
                  </div>
                  <div className="relative px-3 bg-[#16302C]/60 backdrop-blur-sm text-[11px] uppercase tracking-wider text-[#9BAFA9] font-medium rounded-full">
                    or
                  </div>
                </div>

                {/* Google OAuth Button */}
                <button
                  type="button"
                  onClick={handleGoogleAuth}
                  disabled={isLoading || googleLoading}
                  suppressHydrationWarning
                  className="w-full h-11 sm:h-12 px-4 rounded-xl bg-transparent hover:bg-white/[0.04] border border-[rgba(231,239,236,0.18)] hover:border-[rgba(231,239,236,0.35)] text-[#E7EFEC] text-xs sm:text-sm font-medium inline-flex items-center justify-center gap-3 transition-all duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C4622D] cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {googleLoading ? (
                    <span className="inline-flex items-center gap-2">
                      <span className="w-4 h-4 border-2 border-white/20 border-t-white rounded-full animate-spin" />
                      Connecting with Google...
                    </span>
                  ) : (
                    <>
                      <GoogleIcon />
                      Continue with Google
                    </>
                  )}
                </button>
              </form>
            )}
          </div>

          {/* Bottom Switch Link */}
          <div className="flex flex-col items-center gap-2.5">
            <div className="text-center text-xs sm:text-sm text-[#9BAFA9]">
              {isLogin ? (
                <span>
                  Don&apos;t have an account yet?{" "}
                  <button
                    type="button"
                    onClick={() => setAuthMode("register")}
                    suppressHydrationWarning
                    className="text-[#E07A3E] hover:text-[#E7EFEC] font-semibold transition-colors focus:outline-none underline cursor-pointer"
                  >
                    Create a guest account
                  </button>
                </span>
              ) : (
                <span>
                  Already have a guest account?{" "}
                  <button
                    type="button"
                    onClick={() => setAuthMode("login")}
                    suppressHydrationWarning
                    className="text-[#E07A3E] hover:text-[#E7EFEC] font-semibold transition-colors focus:outline-none underline cursor-pointer"
                  >
                    Sign in here
                  </button>
                </span>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
