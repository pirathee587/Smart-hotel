"use client";

import React from "react";
import Link from "next/link";
import { ArrowRight, Leaf, ShieldCheck, Cpu } from "lucide-react";

const gallery = [
  "https://images.unsplash.com/photo-1506929562872-bb421503ef21?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1505118380757-91f5f5632de0?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1519046904884-53103b34b206?auto=format&fit=crop&q=80&w=400",
];

export default function AboutPage() {
  return (
    <div className="flex flex-col w-full text-text-primary bg-bg-dark transition-colors duration-300">
      
      {/* Hero Banner */}
      <section className="relative h-[45vh] w-full flex items-center justify-center overflow-hidden">
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage:
              "url('https://images.unsplash.com/photo-1542314831-068cd1dbfeeb?auto=format&fit=crop&q=80&w=1600')",
          }}
        />
        {/* Soft elegant gradient overlay */}
        <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-transparent to-bg-dark dark:from-black/30 dark:via-black/25 dark:to-[#070c17] transition-all duration-300" />
        <div className="relative z-10 text-center px-6">
          <span className="text-primary text-xs font-semibold tracking-[0.25em] uppercase mb-3 block">
            Our Story
          </span>
          <h1 className="font-serif font-bold text-4xl sm:text-5xl text-[#1B2A4A] dark:text-white tracking-wide">
            About SmartHotel
          </h1>
        </div>
      </section>

      {/* A Luxuries Hotel with Nature */}
      <section className="py-20 max-w-7xl mx-auto px-6 grid gap-16 md:grid-cols-2 items-center animate-in fade-in slide-in-from-bottom-4 duration-800">
        <div>
          <span className="text-primary text-xs font-semibold tracking-[0.2em] uppercase block mb-3">
            Heritage
          </span>
          <h2 className="font-serif font-bold text-3xl text-text-primary leading-tight mb-6">
            A Luxuries Hotel <br />
            with Nature
          </h2>
          <p className="text-text-secondary text-sm leading-relaxed mb-6">
            SmartHotel was founded on the philosophy that true luxury lies in simplicity, convenience, and absolute harmony with our environment. We strive to provide our guests with an uncompromised experience that balances organic natural beauty with modern, digital conveniences.
          </p>
          <p className="text-text-secondary text-sm leading-relaxed mb-8">
            From automated check-in counters and keyless mobile entry to solar-powered infinity pools and locally sourced culinary ingredients, we define the future of sustainable, premium hospitality.
          </p>
        </div>
        
        {/* Right images grid */}
        <div className="grid grid-cols-12 gap-4">
          <div className="col-span-8 overflow-hidden rounded-xl border border-card-border dark:border-white/10 shadow-xl dark:shadow-2xl">
            <img
              src="https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&q=80&w=600"
              alt="Luxury chair on beach"
              className="object-cover w-full h-[300px] transition-transform duration-700 hover:scale-105"
            />
          </div>
          <div className="col-span-4 overflow-hidden rounded-xl border border-card-border dark:border-white/10 shadow-xl dark:shadow-2xl mt-12">
            <img
              src="https://images.unsplash.com/photo-1584132967334-10e028bd69f7?auto=format&fit=crop&q=80&w=600"
              alt="Cozy lounge chair"
              className="object-cover w-full h-[200px] transition-transform duration-700 hover:scale-105"
            />
          </div>
        </div>
      </section>

      {/* Landscape Banner */}
      <section className="relative h-[55vh] w-full flex items-center justify-center overflow-hidden">
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage:
              "url('https://images.unsplash.com/photo-1571896349842-33c89424de2d?auto=format&fit=crop&q=80&w=1600')",
          }}
        />
        <div className="absolute inset-0 bg-black/40" />
        <div className="relative z-10 text-center px-6 max-w-2xl">
          <h2 className="font-serif font-bold text-3xl sm:text-4xl text-white mb-4">
            Uncompromised Relaxation
          </h2>
          <p className="text-slate-200/90 text-sm leading-relaxed">
            Every corner of SmartHotel is curated to offer an escape from the noise. Our infinity pool overlooks the marine sanctuaries, bringing you closer to the tides.
          </p>
        </div>
      </section>

      {/* We Serve Fresh and Delicious Food / Core Values */}
      <section className="py-20 max-w-7xl mx-auto px-6 grid gap-12 md:grid-cols-3">
        <div className="flex flex-col items-center text-center p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md transition-all duration-300">
          <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center text-primary mb-4">
            <Leaf className="size-6" />
          </div>
          <h3 className="font-serif font-semibold text-lg text-text-primary mb-2">100% Sustainable</h3>
          <p className="text-xs text-text-secondary leading-relaxed">
            Equipped with smart solar energy grid systems, water recycling, and green housekeeping practices to safeguard our ecosystems.
          </p>
        </div>

        <div className="flex flex-col items-center text-center p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md transition-all duration-300">
          <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center text-primary mb-4">
            <Cpu className="size-6" />
          </div>
          <h3 className="font-serif font-semibold text-lg text-text-primary mb-2">Smart Living</h3>
          <p className="text-xs text-text-secondary leading-relaxed">
            High-tech automated suites with keyless logins, self check-in desks, and digital butler support at your command.
          </p>
        </div>

        <div className="flex flex-col items-center text-center p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md transition-all duration-300">
          <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center text-primary mb-4">
            <ShieldCheck className="size-6" />
          </div>
          <h3 className="font-serif font-semibold text-lg text-text-primary mb-2">Premium Hospitality</h3>
          <p className="text-xs text-text-secondary leading-relaxed">
            Our resort guides, professional wellness therapists, and Michelin-starred chefs deliver unparalleled personal attention.
          </p>
        </div>
      </section>

      {/* Reservation query */}
      <section className="py-16 bg-gradient-to-r from-primary/10 via-bg-dark to-secondary/10 border-t border-card-border text-center px-6 transition-colors duration-300">
        <div className="max-w-2xl mx-auto flex flex-col items-center">
          <h2 className="font-serif font-bold text-2xl sm:text-3xl text-text-primary mb-3">
            For Reservation or Query?
          </h2>
          <p className="text-text-secondary text-xs max-w-sm leading-relaxed mb-6">
            Get in touch with our booking office to coordinate events, private stays, or custom packages.
          </p>
          <Link href="/contact">
            <button className="px-7 py-2.5 rounded-lg bg-primary hover:bg-primary/95 text-primary-foreground font-semibold text-xs tracking-wider uppercase transition-all duration-300 hover:-translate-y-0.5 shadow-lg shadow-primary/20 cursor-pointer">
              Contact Us Now
            </button>
          </Link>
        </div>
      </section>

      {/* Instagram Snapshots */}
      <section className="grid grid-cols-5 w-full border-b border-card-border">
        {gallery.map((img, idx) => (
          <div key={idx} className="relative overflow-hidden group aspect-square">
            <img
              src={img}
              alt={`Guest snap ${idx}`}
              className="object-cover w-full h-full transition-transform duration-700 group-hover:scale-110"
            />
            <div className="absolute inset-0 bg-black/30 opacity-0 group-hover:opacity-100 transition-opacity duration-300 flex items-center justify-center">
              <span className="text-[10px] text-white uppercase tracking-wider font-semibold">@smarthotel</span>
            </div>
          </div>
        ))}
      </section>
    </div>
  );
}
