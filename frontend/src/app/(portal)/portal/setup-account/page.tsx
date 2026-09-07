'use client'

import React, { useEffect, useState, Suspense } from 'react'
import { useSearchParams, useRouter } from 'next/navigation'
import Link from 'next/link'
import axios from 'axios'
import { Hotel, Lock, CheckCircle2, XCircle, Eye, EyeOff, ShieldCheck, ArrowRight } from 'lucide-react'

interface SetupInfo {
  isValid: boolean
  isAlreadySetup: boolean
  email: string
  customerName: string
  referenceCode: string
}

function SetupAccountContent() {
  const searchParams = useSearchParams()
  const router = useRouter()
  const token = searchParams.get('token')

  const [loading, setLoading] = useState(true)
  const [setupInfo, setSetupInfo] = useState<SetupInfo | null>(null)
  const [errorBanner, setErrorBanner] = useState('')
  const [isAlreadySetup, setIsAlreadySetup] = useState(false)

  // Form states
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState('')
  const [isSuccess, setIsSuccess] = useState(false)

  const apiBase = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

  // ── Password Requirement Checks ─────────────────────────────────────────────
  const hasMinLength = password.length >= 8
  const hasUpperCase = /[A-Z]/.test(password)
  const hasLowerCase = /[a-z]/.test(password)
  const hasNumber = /[0-9]/.test(password)
  const hasSpecialChar = /[^a-zA-Z0-9]/.test(password)
  const isPasswordValid = hasMinLength && hasUpperCase && hasLowerCase && hasNumber && hasSpecialChar
  const isPasswordMatch = confirmPassword.length > 0 && password === confirmPassword

  // ── 1. Validate Token on Mount ──────────────────────────────────────────────
  useEffect(() => {
    if (!token) {
      setErrorBanner('Missing access token. Please check the link from your confirmation email.')
      setLoading(false)
      return
    }

    axios
      .get(`${apiBase}/api/v1/auth/setup-info?token=${encodeURIComponent(token)}`)
      .then((res) => {
        const data: SetupInfo = res.data
        setSetupInfo(data)
        setEmail(data.email)

        if (data.isAlreadySetup) {
          setIsAlreadySetup(true)
          // If already set up, redirect directly to dashboard via portal-access entry
          setTimeout(() => {
            window.location.href = `${apiBase}/api/v1/auth/portal-access?token=${encodeURIComponent(token)}`
          }, 2500)
        }
      })
      .catch((err) => {
        const msg = err.response?.data?.message || 'This guest access link is invalid or has expired.'
        setErrorBanner(msg)
      })
      .finally(() => {
        setLoading(false)
      })
  }, [token, apiBase])

  // ── 2. Handle Form Submit ───────────────────────────────────────────────────
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitError('')

    if (!isPasswordValid) {
      setSubmitError('Please ensure your password meets all 5 security requirements.')
      return
    }

    if (!isPasswordMatch) {
      setSubmitError('Passwords do not match.')
      return
    }

    setSubmitting(true)

    try {
      const res = await axios.post(`${apiBase}/api/v1/auth/setup-account`, {
        token,
        email,
        password,
        confirmPassword,
      })

      const session = res.data
      setIsSuccess(true)

      // Store session token in storage
      if (session.sessionToken) {
        sessionStorage.setItem('portal_session', session.sessionToken)
      }

      // Smooth redirect to portal dashboard
      setTimeout(() => {
        router.push(
          `/portal/dashboard?session=${encodeURIComponent(session.sessionToken)}&bookingId=${session.bookingId}`
        )
      }, 1500)
    } catch (err: any) {
      const msg = err.response?.data?.message || 'Account setup failed. Please check your details and try again.'
      setSubmitError(msg)
      setSubmitting(false)
    }
  }

  // ── State 1: Loading ────────────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[420px] gap-4">
        <div
          className="w-12 h-12 rounded-full border-4 animate-spin"
          style={{ borderColor: 'rgba(226,185,111,0.2)', borderTopColor: '#e2b96f' }}
        />
        <p className="text-sm font-medium tracking-wide" style={{ color: '#a0aec0' }}>
          Validating your secure guest access…
        </p>
      </div>
    )
  }

  // ── State 2: Invalid / Expired Token ────────────────────────────────────────
  if (errorBanner) {
    return (
      <div className="max-w-md mx-auto pt-8">
        <div
          className="rounded-2xl p-8 text-center"
          style={{
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(245,101,101,0.3)',
            backdropFilter: 'blur(12px)',
          }}
        >
          <div className="w-14 h-14 rounded-2xl mx-auto mb-4 flex items-center justify-center bg-red-500/10 text-red-400">
            <XCircle className="w-8 h-8" />
          </div>
          <h2 className="text-xl font-bold text-white mb-2">Access Link Expired or Invalid</h2>
          <p className="text-sm text-slate-300 mb-6 leading-relaxed">{errorBanner}</p>
          <Link
            href="/portal/access"
            className="inline-flex items-center justify-center gap-2 w-full py-3 px-6 rounded-xl font-semibold text-slate-900 transition-all hover:brightness-110"
            style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)' }}
          >
            Look Up Booking Manually <ArrowRight className="w-4 h-4" />
          </Link>
        </div>
      </div>
    )
  }

  // ── State 3: Already Setup ──────────────────────────────────────────────────
  if (isAlreadySetup) {
    return (
      <div className="max-w-md mx-auto pt-8">
        <div
          className="rounded-2xl p-8 text-center"
          style={{
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(72,187,120,0.3)',
            backdropFilter: 'blur(12px)',
          }}
        >
          <div className="w-14 h-14 rounded-2xl mx-auto mb-4 flex items-center justify-center bg-green-500/10 text-green-400">
            <CheckCircle2 className="w-8 h-8" />
          </div>
          <h2 className="text-xl font-bold text-white mb-2">Account Already Set Up</h2>
          <p className="text-sm text-slate-300 mb-6 leading-relaxed">
            Your Guest Portal account for <span className="text-white font-medium">{email}</span> is already active.
            Redirecting you to the dashboard…
          </p>
          <button
            onClick={() => {
              window.location.href = `${apiBase}/api/v1/auth/portal-access?token=${encodeURIComponent(token!)}`
            }}
            className="inline-flex items-center justify-center gap-2 w-full py-3 px-6 rounded-xl font-semibold text-slate-900 transition-all hover:brightness-110"
            style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)' }}
          >
            Continue to Dashboard <ArrowRight className="w-4 h-4" />
          </button>
        </div>
      </div>
    )
  }

  // ── State 4: Success Transition ─────────────────────────────────────────────
  if (isSuccess) {
    return (
      <div className="max-w-md mx-auto pt-8">
        <div
          className="rounded-2xl p-8 text-center animate-in fade-in zoom-in-95 duration-300"
          style={{
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(72,187,120,0.4)',
            backdropFilter: 'blur(12px)',
          }}
        >
          <div className="w-14 h-14 rounded-2xl mx-auto mb-4 flex items-center justify-center bg-green-500/10 text-green-400">
            <CheckCircle2 className="w-8 h-8" />
          </div>
          <h2 className="text-xl font-bold text-white mb-2">Account Created Successfully!</h2>
          <p className="text-sm text-slate-300 mb-4">
            Welcome to SmartHotel, <span className="text-white font-medium">{setupInfo?.customerName}</span>.
          </p>
          <p className="text-xs text-slate-400">
            Redirecting you to your Guest Portal & AI Concierge…
          </p>
        </div>
      </div>
    )
  }

  // ── State 5: Setup Account Form ─────────────────────────────────────────────
  return (
    <div className="max-w-md mx-auto pt-4">
      {/* Booking Reference Badge */}
      {setupInfo?.referenceCode && (
        <div className="flex justify-center mb-4">
          <div
            className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full text-xs font-medium"
            style={{
              background: 'rgba(226,185,111,0.12)',
              border: '1px solid rgba(226,185,111,0.25)',
              color: '#e2b96f',
            }}
          >
            <ShieldCheck className="w-3.5 h-3.5" />
            <span>Booking Ref: {setupInfo.referenceCode}</span>
          </div>
        </div>
      )}

      {/* Main Form Card */}
      <div
        className="rounded-2xl p-8 shadow-2xl border"
        style={{
          background: 'rgba(255, 255, 255, 0.04)',
          borderColor: 'rgba(255, 255, 255, 0.08)',
          backdropFilter: 'blur(16px)',
        }}
      >
        <div className="text-center mb-6">
          <div
            className="w-12 h-12 rounded-xl mx-auto mb-3 flex items-center justify-center"
            style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)' }}
          >
            <Lock className="w-6 h-6 text-slate-900" />
          </div>
          <h1 className="text-xl font-bold text-white">Create Your Guest Portal Account</h1>
          <p className="text-xs text-slate-400 mt-1">
            Set a password to manage your stay, access your digital key, and chat with your AI Concierge.
          </p>
        </div>

        {submitError && (
          <div className="mb-5 p-3.5 rounded-xl text-xs font-medium flex items-center gap-2.5 bg-red-500/10 border border-red-500/25 text-red-400">
            <XCircle className="w-4 h-4 shrink-0" />
            <span>{submitError}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Email (Pre-filled from booking) */}
          <div>
            <label className="block text-xs font-medium mb-1.5 text-slate-300">
              Booking Email
            </label>
            <input
              id="setup-email"
              type="email"
              value={email}
              readOnly
              className="w-full px-4 py-2.5 rounded-xl text-slate-300 text-sm outline-none cursor-not-allowed"
              style={{
                background: 'rgba(255,255,255,0.03)',
                border: '1px solid rgba(255,255,255,0.08)',
              }}
            />
            <p className="text-[11px] text-slate-500 mt-1">
              Pre-filled from your reservation.
            </p>
          </div>

          {/* Password */}
          <div>
            <label className="block text-xs font-medium mb-1.5 text-slate-300">
              Password
            </label>
            <div className="relative">
              <input
                id="setup-password"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter strong password"
                autoComplete="new-password"
                className="w-full px-4 py-2.5 pr-11 rounded-xl text-white text-sm outline-none transition-all placeholder-slate-500"
                style={{
                  background: 'rgba(255,255,255,0.06)',
                  border: '1px solid rgba(255,255,255,0.12)',
                }}
                required
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white transition-colors"
              >
                {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* Real-time Password Requirements */}
          <div
            className="p-3.5 rounded-xl space-y-1.5"
            style={{ background: 'rgba(0,0,0,0.25)', border: '1px solid rgba(255,255,255,0.06)' }}
          >
            <span className="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-2">
              Password Requirements
            </span>
            <div className="grid grid-cols-1 gap-1 text-xs">
              <div className={`flex items-center gap-2 ${hasMinLength ? 'text-green-400' : 'text-slate-500'}`}>
                {hasMinLength ? <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> : <span className="w-3.5 text-center text-[10px]">•</span>}
                <span>At least 8 characters</span>
              </div>
              <div className={`flex items-center gap-2 ${hasUpperCase ? 'text-green-400' : 'text-slate-500'}`}>
                {hasUpperCase ? <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> : <span className="w-3.5 text-center text-[10px]">•</span>}
                <span>One uppercase letter (A-Z)</span>
              </div>
              <div className={`flex items-center gap-2 ${hasLowerCase ? 'text-green-400' : 'text-slate-500'}`}>
                {hasLowerCase ? <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> : <span className="w-3.5 text-center text-[10px]">•</span>}
                <span>One lowercase letter (a-z)</span>
              </div>
              <div className={`flex items-center gap-2 ${hasNumber ? 'text-green-400' : 'text-slate-500'}`}>
                {hasNumber ? <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> : <span className="w-3.5 text-center text-[10px]">•</span>}
                <span>One number (0-9)</span>
              </div>
              <div className={`flex items-center gap-2 ${hasSpecialChar ? 'text-green-400' : 'text-slate-500'}`}>
                {hasSpecialChar ? <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> : <span className="w-3.5 text-center text-[10px]">•</span>}
                <span>One special character (!@#$%^&*)</span>
              </div>
            </div>
          </div>

          {/* Confirm Password */}
          <div>
            <label className="block text-xs font-medium mb-1.5 text-slate-300">
              Confirm Password
            </label>
            <div className="relative">
              <input
                id="setup-confirm-password"
                type={showConfirmPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Re-enter your password"
                autoComplete="new-password"
                className="w-full px-4 py-2.5 pr-11 rounded-xl text-white text-sm outline-none transition-all placeholder-slate-500"
                style={{
                  background: 'rgba(255,255,255,0.06)',
                  border: `1px solid ${
                    confirmPassword.length > 0
                      ? isPasswordMatch
                        ? 'rgba(72,187,120,0.6)'
                        : 'rgba(245,101,101,0.6)'
                      : 'rgba(255,255,255,0.12)'
                  }`,
                }}
                required
              />
              <button
                type="button"
                onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white transition-colors"
              >
                {showConfirmPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            {confirmPassword.length > 0 && (
              <p
                className={`text-[11px] mt-1 font-medium ${
                  isPasswordMatch ? 'text-green-400' : 'text-red-400'
                }`}
              >
                {isPasswordMatch ? '✓ Passwords match' : '✕ Passwords do not match'}
              </p>
            )}
          </div>

          {/* Submit Button */}
          <button
            id="setup-submit-btn"
            type="submit"
            disabled={submitting || !isPasswordValid || !isPasswordMatch}
            className="w-full mt-2 py-3 px-6 rounded-xl font-semibold transition-all shadow-lg flex items-center justify-center gap-2"
            style={{
              background:
                isPasswordValid && isPasswordMatch && !submitting
                  ? 'linear-gradient(135deg, #e2b96f, #d4a054)'
                  : 'rgba(226,185,111,0.25)',
              color: isPasswordValid && isPasswordMatch && !submitting ? '#1a1a2e' : '#718096',
              cursor: isPasswordValid && isPasswordMatch && !submitting ? 'pointer' : 'not-allowed',
            }}
          >
            {submitting ? (
              <>
                <div className="w-4 h-4 border-2 border-slate-900 border-t-transparent rounded-full animate-spin" />
                <span>Creating Account…</span>
              </>
            ) : (
              'Create Account'
            )}
          </button>
        </form>
      </div>
    </div>
  )
}

export default function SetupAccountPage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center min-h-[420px]">
          <div
            className="w-10 h-10 rounded-full border-4 animate-spin"
            style={{ borderColor: 'rgba(226,185,111,0.2)', borderTopColor: '#e2b96f' }}
          />
        </div>
      }
    >
      <SetupAccountContent />
    </Suspense>
  )
}
