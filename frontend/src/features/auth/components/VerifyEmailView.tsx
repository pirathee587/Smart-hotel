"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { AlertCircle,ArrowRight,CheckCircle2,Loader2,Mail,RefreshCw } from "lucide-react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect,useEffectEvent,useState } from "react";

export default function VerifyEmailView() {
  const searchParams = useSearchParams();
  const { verifyEmail, resendVerification, isLoading } = useAuthStore();

  const tokenParam = searchParams.get("token") || "";
  const emailParam = searchParams.get("email") || "";

  const [email, setEmail] = useState(emailParam);
  const [token, setToken] = useState(tokenParam);
  const [status, setStatus] = useState<"idle" | "verifying" | "success" | "error">(
    tokenParam && emailParam ? "verifying" : "idle"
  );
  const [message, setMessage] = useState<string>("");
  const [resendStatus, setResendStatus] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [resendMessage, setResendMessage] = useState<string>("");

  const performVerification = async (verifyEmailAddr: string, verifyToken: string) => {
    setStatus("verifying");
    setMessage("");

    const result = await verifyEmail(verifyEmailAddr, verifyToken);
    if (result.success) {
      setStatus("success");
      setMessage(result.message || "Your email has been successfully verified!");
    } else {
      setStatus("error");
      setMessage(result.error || "Invalid or expired verification link.");
    }
  };
  const performVerificationEvent = useEffectEvent(performVerification);

  useEffect(() => {
    if (!tokenParam || !emailParam) return;
    queueMicrotask(() => {
      setEmail(emailParam);
      setToken(tokenParam);
      void performVerificationEvent(emailParam, tokenParam);
    });
  }, [tokenParam, emailParam]);

  const handleManualSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !token.trim()) {
      setStatus("error");
      setMessage("Please enter both your email address and verification code.");
      return;
    }
    performVerification(email.trim(), token.trim());
  };

  const handleResend = async () => {
    if (!email.trim()) {
      setResendStatus("error");
      setResendMessage("Please provide your email address to receive a new link.");
      return;
    }

    setResendStatus("sending");
    setResendMessage("");
    const result = await resendVerification(email.trim());
    if (result.success) {
      setResendStatus("sent");
      setResendMessage("A new verification link has been sent to your email.");
    } else {
      setResendStatus("error");
      setResendMessage(result.error || "Failed to resend verification link.");
    }
  };

  return (
    <div className="min-h-screen bg-[#0F1B1A] text-[#E7EFEC] flex items-center justify-center p-4 sm:p-6 lg:p-8 selection:bg-[#C4622D]/30 selection:text-[#E7EFEC]">
      <div className="w-full max-w-md bg-[#16302C]/90 backdrop-blur-xl border border-[rgba(231,239,236,0.15)] rounded-2xl p-6 sm:p-8 shadow-2xl relative overflow-hidden">
        {/* Glow decoration */}
        <div className="absolute -top-24 -left-24 w-48 h-48 bg-[#C4622D]/20 rounded-full blur-3xl pointer-events-none" />
        <div className="absolute -bottom-24 -right-24 w-48 h-48 bg-[#16302C]/40 rounded-full blur-3xl pointer-events-none" />

        {/* Brand Header */}
        <div className="text-center mb-8 relative z-10">
          <Link href="/" className="inline-block text-2xl font-bold tracking-tight mb-2">
            Smart<span className="text-[#C4622D] italic font-serif">Hotel</span>
          </Link>
          <p className="text-xs uppercase tracking-widest text-[#9BAFA9]">Account Verification</p>
        </div>

        {/* Status: VERIFYING */}
        {status === "verifying" && (
          <div className="text-center py-8 relative z-10">
            <Loader2 className="w-12 h-12 text-[#C4622D] animate-spin mx-auto mb-4" />
            <h2 className="text-xl font-semibold mb-2">Verifying Your Account</h2>
            <p className="text-sm text-[#9BAFA9]">Please wait while we confirm your email verification link...</p>
          </div>
        )}

        {/* Status: SUCCESS */}
        {status === "success" && (
          <div className="text-center py-6 relative z-10 space-y-4">
            <div className="w-16 h-16 bg-emerald-500/10 border border-emerald-500/30 rounded-full flex items-center justify-center mx-auto text-emerald-400">
              <CheckCircle2 className="w-9 h-9" />
            </div>
            <h2 className="text-2xl font-semibold text-[#E7EFEC]">Email Verified!</h2>
            <p className="text-sm text-[#9BAFA9] max-w-xs mx-auto">
              {message || "Your email has been verified. You can now access your account and explore luxury stays."}
            </p>

            <div className="pt-4">
              <Link
                href="/login"
                className="w-full inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-xl bg-[#C4622D] hover:bg-[#A84F20] text-white font-medium shadow-lg shadow-[#C4622D]/25 transition-all duration-200"
              >
                Sign In Now <ArrowRight className="w-4 h-4" />
              </Link>
            </div>
          </div>
        )}

        {/* Status: ERROR */}
        {status === "error" && (
          <div className="py-4 relative z-10 space-y-5">
            <div className="w-14 h-14 bg-red-500/10 border border-red-500/30 rounded-full flex items-center justify-center mx-auto text-red-400">
              <AlertCircle className="w-8 h-8" />
            </div>
            <div className="text-center">
              <h2 className="text-xl font-semibold text-[#E7EFEC]">Verification Failed</h2>
              <p className="text-sm text-red-300 mt-1">{message}</p>
            </div>

            <div className="border-t border-white/10 pt-4">
              <p className="text-xs text-[#9BAFA9] mb-3 text-center">
                Need a new verification link? Enter your email address below:
              </p>
              <div className="flex gap-2">
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@example.com"
                  className="flex-1 px-3.5 py-2.5 rounded-lg bg-[#0F1B1A] border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#9BAFA9]/60 text-sm focus:outline-none focus:border-[#C4622D]"
                />
                <button
                  type="button"
                  onClick={handleResend}
                  disabled={resendStatus === "sending"}
                  className="px-4 py-2.5 bg-[#C4622D] hover:bg-[#A84F20] disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors flex items-center gap-1.5"
                >
                  {resendStatus === "sending" ? (
                    <RefreshCw className="w-4 h-4 animate-spin" />
                  ) : (
                    "Resend"
                  )}
                </button>
              </div>

              {resendMessage && (
                <p
                  className={`text-xs mt-2 text-center ${
                    resendStatus === "sent" ? "text-emerald-400" : "text-red-400"
                  }`}
                >
                  {resendMessage}
                </p>
              )}
            </div>

            <div className="pt-2 text-center">
              <Link href="/login" className="text-sm text-[#C4622D] hover:underline">
                Return to Sign In
              </Link>
            </div>
          </div>
        )}

        {/* Status: IDLE (manual entry) */}
        {status === "idle" && (
          <form onSubmit={handleManualSubmit} className="space-y-4 relative z-10">
            <div className="w-12 h-12 bg-[#C4622D]/10 border border-[#C4622D]/20 rounded-full flex items-center justify-center mx-auto text-[#C4622D] mb-2">
              <Mail className="w-6 h-6" />
            </div>
            <div className="text-center mb-4">
              <h2 className="text-lg font-medium text-[#E7EFEC]">Verify Email Address</h2>
              <p className="text-xs text-[#9BAFA9] mt-1">
                Enter your email address and verification code below:
              </p>
            </div>

            <div>
              <label className="block text-xs font-medium text-[#9BAFA9] mb-1">Email Address</label>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="name@example.com"
                className="w-full px-3.5 py-2.5 rounded-lg bg-[#0F1B1A] border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] text-sm focus:outline-none focus:border-[#C4622D]"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-[#9BAFA9] mb-1">Verification Code / Token</label>
              <input
                type="text"
                required
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder="Paste your 32-character token"
                className="w-full px-3.5 py-2.5 rounded-lg bg-[#0F1B1A] border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] text-sm font-mono text-xs focus:outline-none focus:border-[#C4622D]"
              />
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 bg-[#C4622D] hover:bg-[#A84F20] disabled:opacity-50 text-white font-medium rounded-xl text-sm transition-colors flex items-center justify-center gap-2"
            >
              {isLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : "Verify Account"}
            </button>

            <div className="text-center pt-2">
              <button
                type="button"
                onClick={handleResend}
                className="text-xs text-[#9BAFA9] hover:text-[#E7EFEC] underline"
              >
                Didn&apos;t receive a link? Send again
              </button>
              {resendMessage && (
                <p
                  className={`text-xs mt-1.5 ${
                    resendStatus === "sent" ? "text-emerald-400" : "text-red-400"
                  }`}
                >
                  {resendMessage}
                </p>
              )}
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
