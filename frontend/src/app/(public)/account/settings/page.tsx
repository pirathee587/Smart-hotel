import GuestSettingsView from "@/features/settings/components/GuestSettingsView";
import { Metadata } from "next";

export const metadata: Metadata = {
  title: "Guest Settings — SmartHotel Maskeliya",
  description: "Manage your guest sanctuary profile, security credentials, wallet balances, and preferences.",
};

export default function GuestSettingsPage() {
  return (
    <main className="min-h-screen bg-[#F7F6F2] py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-5xl mx-auto">
        <div className="mb-8">
          <h1
            className="font-serif text-3xl sm:text-4xl text-[#0F1B1A] font-semibold tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Guest Sanctuary Account
          </h1>
          <p className="text-xs sm:text-sm text-[#0F1B1A]/70 mt-1.5 max-w-2xl leading-relaxed">
            Manage your personal sanctuary credentials, security passkeys, currency preferences, and past stay reviews.
          </p>
        </div>

        <GuestSettingsView isDrawer={false} />
      </div>
    </main>
  );
}
