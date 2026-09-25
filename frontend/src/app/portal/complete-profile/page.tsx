"use client";

import { ImageUploadDropzone } from "@/components/ui/ImageUploadDropzone";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import api from "@/lib/axios";
import { AlertTriangle,Building2,Clock3,Loader2,ShieldCheck } from "lucide-react";
import { useRouter } from "next/navigation";
import { FormEvent,useEffect,useState } from "react";

const fields = [
  ["nationalId", "NIC number", "Enter the NIC or national identity number"],
  ["bankName", "Bank name", "Enter the bank name"],
  ["bankAccountName", "Account holder", "Enter the account holder name"],
  ["bankAccountNumber", "Account number", "Enter the bank account number"],
  ["bankBranch", "Bank branch", "Enter the branch name"],
] as const;

type Profile = {
  fullName: string;
  email: string;
  role: string;
  status: string;
  departmentName?: string;
  profileCompletionDeadlineUtc?: string;
};

type ApiError = {
  response?: { data?: { message?: string } };
};

export default function CompleteProfilePage() {
  const router = useRouter();
  const { user, isAuthenticated, initialize, updateAfterPasswordReset } = useAuthStore();
  const [authReady, setAuthReady] = useState(false);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [form, setForm] = useState({ nationalId: "", bankName: "", bankAccountName: "", bankAccountNumber: "", bankBranch: "", profilePhotoUrl: "", nicPhotoUrl: "" });
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let active = true;
    void initialize().finally(() => { if (active) setAuthReady(true); });
    return () => { active = false; };
  }, [initialize]);
  useEffect(() => {
    if (!authReady) return;
    if (!isAuthenticated) {
      router.replace("/staff/login");
      return;
    }
    const role = String(user?.role ?? "");
    if (role !== "Admin") {
      router.replace(role === "Owner" ? "/owner" : "/dashboard");
      return;
    }
    let active = true;
    void api.get<Profile>("/api/v1/employees/me/profile")
      .then(({ data }) => { if (active) setProfile(data); })
      .catch((requestError: ApiError) => {
        if (active) setError(requestError.response?.data?.message || "Could not load your employee profile.");
      })
      .finally(() => { if (active) setLoadingProfile(false); });
    return () => { active = false; };
  }, [authReady, isAuthenticated, router, user?.role]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (!form.profilePhotoUrl || !form.nicPhotoUrl || fields.some(([key]) => !form[key].trim())) {
      setError("Profile picture, NIC photo, NIC number and all bank details are required.");
      return;
    }
    setSaving(true);
    try {
      const { data } = await api.put("/api/v1/employees/me/profile", form);
      updateAfterPasswordReset(data.accessToken, false);
      router.replace("/dashboard");
    } catch (requestError: unknown) {
      const apiError = requestError as ApiError;
      setError(apiError.response?.data?.message || "Could not save your profile.");
    } finally {
      setSaving(false);
    }
  }

  const deadline = profile?.profileCompletionDeadlineUtc || user?.profileCompletionDeadlineUtc;

  return (
    <main className="staff-portal-theme min-h-screen bg-[#0F1B1A] px-4 py-8 text-white md:px-8 md:py-12">
      <form onSubmit={submit} className="mx-auto max-w-6xl space-y-7 rounded-3xl border border-[#29413d] bg-[#15332f] p-6 shadow-2xl md:p-10">
        <header className="flex flex-col gap-4 border-b border-white/10 pb-7 md:flex-row md:items-start md:justify-between">
          <div>
            <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-[#9BC5B8]"><ShieldCheck className="h-4 w-4" />Administrator onboarding</div>
            <h1 className="text-3xl font-bold md:text-4xl">Complete your administrator profile</h1>
            <p className="mt-3 max-w-3xl text-sm leading-6 text-[#b3c5c0]">Your permanent password has been created. Complete every field and upload both required images before entering the staff portal.</p>
          </div>
        </header>

        <section className="rounded-2xl border border-[#E87332]/45 bg-[#E87332]/10 p-5">
          <div className="flex items-start gap-3">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-[#F28A4B]" />
            <div>
              <h2 className="font-semibold text-[#FFD2B8]">Complete this profile within two days</h2>
              <p className="mt-1 text-sm leading-6 text-[#E9B99F]">Portal access remains restricted until all details are submitted. If the profile is not completed before the deadline, your account will be automatically deactivated and an Owner must reactivate it.</p>
              {deadline && <p className="mt-3 flex items-center gap-2 text-sm font-semibold text-[#F28A4B]"><Clock3 className="h-4 w-4" />Deadline: {new Date(deadline).toLocaleString()}</p>}
            </div>
          </div>
        </section>

        {error && <div className="rounded-xl border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">{error}</div>}

        <section className="rounded-2xl border border-white/10 bg-[#0F1B1A]/35 p-5 md:p-6">
          <h2 className="mb-5 text-xs font-semibold uppercase tracking-wider text-[#9BC5B8]">Account information</h2>
          {loadingProfile ? <div className="flex items-center gap-2 text-sm text-[#9BAFA9]"><Loader2 className="h-4 w-4 animate-spin" />Loading account details…</div> : (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {[
                ["Full name", profile?.fullName || user?.name || "—"],
                ["Email", profile?.email || user?.email || "—"],
                ["Department", profile?.departmentName || "Unassigned"],
                ["Status", profile?.status || "Active"],
              ].map(([label, value]) => <div key={label}><p className="mb-2 text-sm text-[#9BC5B8]">{label}</p><div className="min-h-12 rounded-xl border border-white/5 bg-[#0e211f] px-4 py-3 text-sm text-[#D7E2DF]">{value}</div></div>)}
            </div>
          )}
        </section>

        <section className="grid gap-6 md:grid-cols-2">
          <div className="rounded-2xl border border-white/10 bg-[#0F1B1A]/35 p-5"><h2 className="mb-4 text-xs font-semibold uppercase tracking-wider text-[#9BC5B8]">Profile picture <span className="text-[#F28A4B]">*</span></h2><ImageUploadDropzone category="employees" label="Upload profile picture" onUploadComplete={(url) => setForm(value => ({ ...value, profilePhotoUrl: url }))} /></div>
          <div className="rounded-2xl border border-white/10 bg-[#0F1B1A]/35 p-5"><h2 className="mb-4 text-xs font-semibold uppercase tracking-wider text-[#9BC5B8]">NIC photo <span className="text-[#F28A4B]">*</span></h2><ImageUploadDropzone category="employees" label="Upload NIC photo" onUploadComplete={(url) => setForm(value => ({ ...value, nicPhotoUrl: url }))} /></div>
        </section>

        <section className="rounded-2xl border border-white/10 bg-[#0F1B1A]/35 p-5 md:p-6">
          <h2 className="mb-5 flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-[#9BC5B8]"><Building2 className="h-4 w-4" />Identity and bank details</h2>
          <div className="grid gap-5 md:grid-cols-2">
            {fields.map(([key, label, placeholder]) => <label key={key} className="text-sm text-[#b3c5c0]">{label} <span className="text-[#F28A4B]">*</span><input required autoComplete="off" placeholder={placeholder} value={form[key]} onChange={event => setForm(value => ({ ...value, [key]: event.target.value }))} className="mt-2 w-full rounded-xl border border-[#35504b] bg-[#0e211f] px-4 py-3 text-white outline-none placeholder:text-white/25 focus:border-[#C4622D] focus:ring-2 focus:ring-[#C4622D]/20" /></label>)}
          </div>
        </section>

        <div className="flex flex-col items-start justify-between gap-4 border-t border-white/10 pt-6 sm:flex-row sm:items-center">
          <p className="max-w-2xl text-xs leading-5 text-[#8FA5A0]">All fields marked * are mandatory. Verify the NIC and bank details carefully before submitting.</p>
          <button disabled={saving || loadingProfile} className="flex min-w-56 items-center justify-center gap-2 rounded-xl bg-[#C4622D] px-7 py-3 font-semibold hover:bg-[#db7138] disabled:cursor-not-allowed disabled:opacity-50">{saving ? <><Loader2 className="h-4 w-4 animate-spin" />Saving profile…</> : "Save profile & open portal"}</button>
        </div>
      </form>
    </main>
  );
}
