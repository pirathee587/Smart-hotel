"use client";

import React from "react";
import Link from "next/link";
import { Compass, Wine, Anchor, Sparkles } from "lucide-react";
import { cn } from "@/lib/utils";

const services = [
  {
    title: "Seaside Infinity Pools",
    description: "Relax on our floating decks, swim overlooking the oceans, or coordinate private poolside drinks.",
    img: "https://images.unsplash.com/photo-1571896349842-33c89424de2d?auto=format&fit=crop&q=80&w=800",
    icon: Anchor,
  },
  {
    title: "Michelin Fine Dining",
    description: "Indulge in aged steak cuts, sea-fresh plates, and luxury wine collections curated by our sommeliers.",
    img: "https://images.unsplash.com/photo-1578683010236-d716f9a3f461?auto=format&fit=crop&q=80&w=800",
    icon: Wine,
  },
  {
    title: "Signature Thermal Spa",
    description: "Rejuvenate with botanical facials, hot oil massages, and thermal mineral pools guided by experts.",
    img: "https://images.unsplash.com/photo-1540555700478-4be289fbecef?auto=format&fit=crop&q=80&w=800",
    icon: Sparkles,
  },
  {
    title: "Private Yacht Charters",
    description: "Explore the islands and marine sanctuaries with private yachts and curated sunset sailing packages.",
    img: "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&q=80&w=800",
    icon: Compass,
  },
];

export default function ServicesPage() {
  return (
    <div className="flex flex-col w-full text-text-primary bg-bg-dark transition-colors duration-300">
      
      {/* Hero Banner */}
      <section className="relative h-[45vh] w-full flex items-center justify-center overflow-hidden">
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage:
              "url('https://images.unsplash.com/photo-1571896349842-33c89424de2d?auto=format&fit=crop&q=80&w=1600')",
          }}
        />
        <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-transparent to-bg-dark dark:from-black/30 dark:via-black/25 dark:to-[#070c17] transition-all duration-300" />
        <div className="relative z-10 text-center px-6">
          <span className="text-primary text-xs font-semibold tracking-[0.25em] uppercase mb-3 block">
            Resort Facilities
          </span>
          <h1 className="font-serif font-bold text-4xl sm:text-5xl text-[#1B2A4A] dark:text-white tracking-wide">
            Services & Amenities
          </h1>
        </div>
      </section>

      {/* Services Grid Display */}
      <section className="py-20 max-w-7xl mx-auto px-6 w-full space-y-16">
        <div className="text-center max-w-xl mx-auto">
          <span className="text-primary text-xs font-semibold tracking-[0.2em] uppercase block mb-3">
            Bespoke Services
          </span>
          <h2 className="font-serif font-bold text-3xl text-text-primary leading-tight">
            Guest Conveniences
          </h2>
          <p className="text-text-secondary text-xs mt-3">
            Every facility is wired for high-speed connection and curated with personal care.
          </p>
        </div>

        <div className="grid gap-8 md:grid-cols-2">
          {services.map((svc, idx) => {
            const Icon = svc.icon;
            return (
              <Card key={idx} className="flex flex-col overflow-hidden hover:border-primary/25 hover:shadow-primary/5 group">
                <div className="relative aspect-[16/9] overflow-hidden bg-slate-900">
                  <img
                    src={svc.img}
                    alt={svc.title}
                    className="object-cover w-full h-full transition-transform duration-700 group-hover:scale-103"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
                  
                  {/* Floating Icon */}
                  <div className="absolute top-4 left-4 w-10 h-10 rounded-lg bg-primary/25 border border-primary/40 text-primary flex items-center justify-center backdrop-blur-md">
                    <Icon className="size-5" />
                  </div>
                </div>
                <div className="p-6">
                  <h3 className="font-serif font-bold text-lg text-text-primary mb-2">
                    {svc.title}
                  </h3>
                  <p className="text-xs text-text-secondary leading-relaxed">
                    {svc.description}
                  </p>
                </div>
              </Card>
            );
          })}
        </div>
      </section>

      {/* Testimonials */}
      <section className="py-20 bg-section-alt border-y border-card-border text-center px-6 transition-colors duration-300">
        <div className="max-w-2xl mx-auto">
          <span className="text-primary text-xs font-semibold tracking-[0.2em] uppercase block mb-3">
            Testimonials
          </span>
          <h2 className="font-serif font-bold text-2xl sm:text-3xl text-text-primary mb-8">
            What Our Guests Say
          </h2>
          <div className="space-y-4">
            <p className="font-serif italic text-lg text-text-primary opacity-90">
              &ldquo;An absolutely magical experience. The staff check-in was seamless, the spa was breathtaking, and the ocean vistas from our pool deck were unforgettable. SmartHotel is our new home away from home.&rdquo;
            </p>
            <div className="text-xs text-text-secondary font-semibold uppercase tracking-wider">
              &mdash; Sarah & David Miller, London
            </div>
          </div>
        </div>
      </section>

      {/* Reservation queries */}
      <section className="py-16 bg-gradient-to-r from-primary/10 via-bg-dark to-secondary/10 text-center px-6 transition-colors duration-300">
        <div className="max-w-2xl mx-auto flex flex-col items-center">
          <h2 className="font-serif font-bold text-2xl sm:text-3xl text-text-primary mb-3">
            Ready to Begin Your Escape?
          </h2>
          <p className="text-text-secondary text-xs max-w-sm leading-relaxed mb-6">
            Book now or coordinate directly with our team to configure your private event package.
          </p>
          <Link href="/login">
            <button className="px-7 py-2.5 rounded-lg bg-primary hover:bg-primary/95 text-white font-semibold text-xs tracking-wider uppercase transition-all duration-300 hover:-translate-y-0.5 shadow-lg shadow-primary/20 cursor-pointer">
              Book A Room
            </button>
          </Link>
        </div>
      </section>
    </div>
  );
}

function Card({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn("bg-card-dark dark:bg-[#121926]/40 border border-card-border dark:border-white/5 rounded-xl shadow-lg dark:shadow-xl transition-all duration-300", className)}>
      {children}
    </div>
  );
}
