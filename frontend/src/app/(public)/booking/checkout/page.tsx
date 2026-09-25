import { CheckoutPageContent } from "@/features/bookings/components/CheckoutPageContent";
import { Metadata } from "next";
import { Suspense } from "react";

export const metadata: Metadata = {
  title: "Checkout & Reservation | SmartHotel Maskeliya",
  description:
    "Complete your direct boutique sanctuary reservation at SmartHotel Maskeliya. Secure tokenized checkout with member perks and best rate guarantee.",
};

export default function CheckoutPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-[#F7F6F2] flex items-center justify-center py-20">
          <div className="flex flex-col items-center gap-3">
            <div className="w-10 h-10 rounded-full border-3 border-[#E07A3E] border-t-transparent animate-spin" />
            <p className="text-sm font-medium text-[#0F1B1A]/70">
              Loading checkout details...
            </p>
          </div>
        </div>
      }
    >
      <CheckoutPageContent />
    </Suspense>
  );
}
