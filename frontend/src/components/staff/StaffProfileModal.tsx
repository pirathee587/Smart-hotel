"use client";

import { ImageUploadDropzone } from "@/components/ui/ImageUploadDropzone";
import { dashboardApi,type EmployeeProfileDto } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import { Ban,Eye,KeyRound,Loader2,Power,Save,Trash2,X } from "lucide-react";
import Image from "next/image";
import { useState } from "react";

export function StaffProfileModal({ employee, queryKey, onClose, onCredentials, canEditProfile = true }: {
  employee: { id: string; email: string; fullName?: string };
  queryKey: string;
  onClose: () => void;
  onCredentials: (value: { email: string; temporaryPassword: string; message: string }) => void;
  canEditProfile?: boolean;
}) {
  const qc = useQueryClient();
  const [suspensionReason, setSuspensionReason] = useState("");
  const [showNicPreview, setShowNicPreview] = useState(false);
  const [showProfilePreview, setShowProfilePreview] = useState(false);
  const profile = useQuery({ queryKey: ["staff-profile", employee.id], queryFn: () => dashboardApi.getEmployeeProfile(employee.id) });
  const refresh = async () => { await profile.refetch(); await qc.invalidateQueries({ queryKey: [queryKey] }); await qc.invalidateQueries({ queryKey: ["employees"] }); };
  const update = useMutation({ mutationFn: (payload: Partial<EmployeeProfileDto>) => dashboardApi.updateEmployeeProfile(employee.id, payload), onSuccess: refresh });
  const suspend = useMutation({ mutationFn: () => dashboardApi.suspendEmployee(employee.id, suspensionReason), onSuccess: refresh });
  const reactivate = useMutation({ mutationFn: () => dashboardApi.reactivateEmployee(employee.id), onSuccess: refresh });
  const resend = useMutation({ mutationFn: () => dashboardApi.resendEmployeeCredentials(employee.id), onSuccess: (data) => onCredentials({ email: employee.email, ...data }) });
  const remove = useMutation({ mutationFn: () => dashboardApi.deleteEmployee(employee.id), onSuccess: async () => { await qc.invalidateQueries({ queryKey: [queryKey] }); await qc.invalidateQueries({ queryKey: ["employees"] }); onClose(); } });
  const p = profile.data;
  const busy = update.isPending || suspend.isPending || reactivate.isPending || resend.isPending || remove.isPending;

  const save = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    update.mutate({ nationalId: String(data.get("nationalId") || ""), bankName: String(data.get("bankName") || ""), bankAccountName: String(data.get("bankAccountName") || ""), bankAccountNumber: String(data.get("bankAccountNumber") || ""), bankBranch: String(data.get("bankBranch") || ""), nicPhotoUrl: p?.nicPhotoUrl, profilePhotoUrl: p?.profilePhotoUrl });
  };

  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"><div className="max-h-[92vh] w-full max-w-3xl overflow-y-auto rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] shadow-2xl">
    <div className="sticky top-0 z-10 flex items-center justify-between border-b border-[var(--card-border)] bg-[#16302C] p-5"><div><h2 className="font-semibold text-white">{employee.fullName || employee.email}</h2><p className="text-xs text-white/60">Administrator / employee profile</p></div><button onClick={onClose} className="rounded-lg p-2 text-white/70 hover:bg-white/5"><X className="h-5 w-5" /></button></div>
    {profile.isLoading ? <div className="flex justify-center p-16"><Loader2 className="h-7 w-7 animate-spin text-[#E87332]" /></div> : !p ? <p className="p-8 text-[#F28A4B]">Unable to load profile.</p> : <form onSubmit={save} className="space-y-6 p-5 sm:p-6">
      <div className="rounded-2xl border border-white/10 bg-[#0F1B1A]/45 p-4"><p className="mb-3 text-xs font-semibold uppercase tracking-wider text-[var(--text-secondary)]">Profile picture</p><div className="flex flex-col gap-4 sm:flex-row sm:items-center">{p.profilePhotoUrl ? <button type="button" onClick={() => setShowProfilePreview(true)} className="relative h-24 w-24 shrink-0 overflow-hidden rounded-full border-2 border-[#E87332]/40"><Image src={p.profilePhotoUrl} alt="Profile picture" fill className="object-cover" unoptimized /></button> : <div className="flex h-24 w-24 shrink-0 items-center justify-center rounded-full border-2 border-dashed border-white/15 text-2xl font-bold text-white/40">{p.fullName.split(/\s+/).slice(0,2).map((part) => part[0]).join("")}</div>}<div className="flex-1">{canEditProfile ? <ImageUploadDropzone category="employees" label={p.profilePhotoUrl ? "Change profile picture" : "Upload profile picture"} onUploadComplete={(profilePhotoUrl) => update.mutate({ profilePhotoUrl })} /> : p.profilePhotoUrl ? <button type="button" onClick={() => setShowProfilePreview(true)} className="flex items-center gap-2 rounded-xl border border-[#E87332]/25 px-4 py-2 text-xs text-[#F28A4B]"><Eye className="h-4 w-4" />View profile picture</button> : <p className="text-xs text-white/45">No profile picture uploaded</p>}</div></div></div>
      <div className="grid gap-5 md:grid-cols-[220px_1fr]">
        <div className="space-y-3"><p className="text-xs font-semibold uppercase tracking-wider text-[var(--text-secondary)]">NIC photo</p>{p.nicPhotoUrl ? <button type="button" onClick={() => setShowNicPreview(true)} className="group relative block h-32 w-full overflow-hidden rounded-xl border border-white/10"><Image src={p.nicPhotoUrl} alt="NIC document" fill className="object-cover transition-transform group-hover:scale-105" unoptimized /><span className="absolute inset-0 flex items-center justify-center bg-black/0 text-sm font-medium text-white opacity-0 transition-all group-hover:bg-black/55 group-hover:opacity-100"><Eye className="mr-2 h-4 w-4" />View photo</span></button> : <div className="flex h-32 items-center justify-center rounded-xl border border-dashed border-white/10 text-xs text-white/45">No NIC photo uploaded</div>}<button type="button" disabled={!p.nicPhotoUrl} onClick={() => setShowNicPreview(true)} className="flex w-full items-center justify-center gap-2 rounded-xl border border-[#E87332]/25 px-3 py-2 text-xs text-[#F28A4B] hover:bg-[#C4622D]/10 disabled:cursor-not-allowed disabled:border-white/10 disabled:text-white/30 disabled:hover:bg-transparent"><Eye className="h-4 w-4" />View NIC photo</button>{canEditProfile && <ImageUploadDropzone category="employees" label="Upload NIC photo" onUploadComplete={(nicPhotoUrl) => update.mutate({ nicPhotoUrl })} />}</div>
        <div className="grid gap-4 sm:grid-cols-2"><Field label="Full name" value={p.fullName} disabled /><Field label="Email" value={p.email} disabled /><Field label="Department" value={p.departmentName || "Unassigned"} disabled /><Field label="Status" value={p.status} disabled /><Field name="nationalId" label="NIC number" value={p.nationalId} disabled={!canEditProfile} /><Field name="bankName" label="Bank name" value={p.bankName} disabled={!canEditProfile} /><Field name="bankAccountName" label="Account holder" value={p.bankAccountName} disabled={!canEditProfile} /><Field name="bankAccountNumber" label="Account number" value={p.bankAccountNumber} disabled={!canEditProfile} /><Field name="bankBranch" label="Bank branch" value={p.bankBranch} disabled={!canEditProfile} /></div>
      </div>
      {canEditProfile && <button disabled={busy} className="flex items-center gap-2 rounded-xl bg-[#9F4724] px-4 py-2.5 text-sm text-white hover:bg-[#C4622D] disabled:opacity-50"><Save className="h-4 w-4" />Save profile</button>}
      <div className="border-t border-white/10 pt-5"><h3 className="mb-3 text-sm font-semibold text-white">Account controls</h3><div className="flex flex-wrap gap-2"><button type="button" disabled={busy || !p.isActive} onClick={() => resend.mutate()} className="flex items-center gap-2 rounded-xl border border-[#E87332]/30 px-3 py-2 text-xs text-[#F28A4B]"><KeyRound className="h-4 w-4" />Resend credentials</button>{p.isActive ? <><input value={suspensionReason} onChange={(e) => setSuspensionReason(e.target.value)} placeholder="Suspension reason (required)" className="min-w-56 flex-1 rounded-xl border border-white/10 bg-[#0F1B1A] px-3 py-2 text-xs text-white" /><button type="button" disabled={busy || !suspensionReason.trim()} onClick={() => suspend.mutate()} className="flex items-center gap-2 rounded-xl bg-amber-700 px-3 py-2 text-xs text-white disabled:opacity-40"><Ban className="h-4 w-4" />Suspend</button></> : <button type="button" disabled={busy} onClick={() => reactivate.mutate()} className="flex items-center gap-2 rounded-xl bg-emerald-700 px-3 py-2 text-xs text-white"><Power className="h-4 w-4" />Reactivate</button>}<button type="button" disabled={busy} onClick={() => window.confirm(`Permanently delete ${p.fullName}?`) && remove.mutate()} className="flex items-center gap-2 rounded-xl bg-red-800 px-3 py-2 text-xs text-white"><Trash2 className="h-4 w-4" />Delete</button></div>{p.suspensionReason && <p className="mt-3 rounded-xl border border-amber-500/20 bg-amber-500/10 p-3 text-xs text-amber-200"><strong>Suspension reason:</strong> {p.suspensionReason}</p>}</div>
    </form>}
    {showNicPreview && p?.nicPhotoUrl && <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/90 p-4" onClick={() => setShowNicPreview(false)}><div className="relative h-[75vh] w-full max-w-5xl" onClick={(event) => event.stopPropagation()}><Image src={p.nicPhotoUrl} alt="Full-size NIC document" fill className="object-contain" unoptimized /><button type="button" onClick={() => setShowNicPreview(false)} className="absolute right-2 top-2 rounded-full bg-black/70 p-2 text-white hover:bg-black"><X className="h-5 w-5" /></button></div></div>}
    {showProfilePreview && p?.profilePhotoUrl && <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/90 p-4" onClick={() => setShowProfilePreview(false)}><div className="relative h-[75vh] w-full max-w-4xl" onClick={(event) => event.stopPropagation()}><Image src={p.profilePhotoUrl} alt="Full-size profile picture" fill className="object-contain" unoptimized /><button type="button" onClick={() => setShowProfilePreview(false)} className="absolute right-2 top-2 rounded-full bg-black/70 p-2 text-white hover:bg-black"><X className="h-5 w-5" /></button></div></div>}
  </div></div>;
}

function Field({ label, value, name, disabled }: { label: string; value?: string; name?: string; disabled?: boolean }) {
  return <label className="block text-xs text-[var(--text-secondary)]">{label}<input name={name} defaultValue={value || ""} disabled={disabled} className="mt-1 w-full rounded-xl border border-white/10 bg-[#0F1B1A] px-3 py-2.5 text-sm text-white disabled:opacity-65" /></label>;
}
