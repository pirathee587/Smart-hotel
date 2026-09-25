import AuthView from "@/features/auth/components/AuthView";
import { Metadata } from "next";
import { Suspense } from "react";

export const metadata: Metadata = {
  title: "Sign in — SmartHotel Maskeliya",
  description: "Sign in to manage your stay, view personalized services, or access the staff dashboard.",
};

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-[#0F1B1A]" />}>
      <AuthView mode="login" />
    </Suspense>
  );
}
