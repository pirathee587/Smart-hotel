"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { Star } from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/axios";

const offers = [
  {
    title: "Up to 35% savings on Club rooms and Suites",
    image: "https://images.unsplash.com/photo-1520250497591-112f2f40a3f4?auto=format&fit=crop&q=80&w=600",
    features: ["Luxury Accommodation", "Breakfast included", "Free wifi & lounge access"],
  },
  {
    title: "Up to 30% savings on Spa Treatments & Retreats",
    image: "https://images.unsplash.com/photo-1540555700478-4be289fbecef?auto=format&fit=crop&q=80&w=600",
    features: ["Professional therapists", "Organic wellness treatments", "Complimentary herbal tea"],
  },
  {
    title: "Up to 25% savings on Dining & Gourmet Packages",
    image: "https://images.unsplash.com/photo-1578683010236-d716f9a3f461?auto=format&fit=crop&q=80&w=600",
    features: ["Michelin-starred chefs", "Wine pairing options", "Candlelight shoreline seating"],
  },
];

interface RoomItem {
  name: string;
  price: string;
  image: string;
}

const defaultRooms: RoomItem[] = [
  {
    name: "Superior Room",
    price: "$120",
    image: "https://images.unsplash.com/photo-1566665797739-1674de7a421a?auto=format&fit=crop&q=80&w=600",
  },
  {
    name: "Deluxe Room",
    price: "$150",
    image: "https://images.unsplash.com/photo-1590490360182-c33d57733427?auto=format&fit=crop&q=80&w=600",
  },
  {
    name: "Signature Suite",
    price: "$220",
    image: "https://images.unsplash.com/photo-1582719508461-905c673771fd?auto=format&fit=crop&q=80&w=600",
  },
  {
    name: "Couple Retreat Room",
    price: "$180",
    image: "https://images.unsplash.com/photo-1596394516093-501ba68a0ba6?auto=format&fit=crop&q=80&w=600",
  },
];

const gallery = [
  "https://images.unsplash.com/photo-1506929562872-bb421503ef21?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1505118380757-91f5f5632de0?auto=format&fit=crop&q=80&w=400",
  "https://images.unsplash.com/photo-1519046904884-53103b34b206?auto=format&fit=crop&q=80&w=400",
];

export default function RoomsPage() {
  const [rooms, setRooms] = useState<RoomItem[]>(defaultRooms);

  useEffect(() => {
    async function loadRoomTypes() {
      try {
        const res = await api.get("/api/v1/room-types");
        if (Array.isArray(res.data) && res.data.length > 0) {
          const mapped: RoomItem[] = res.data.map((r: any, idx: number) => ({
            name: r.name || r.title,
            price: `$${r.pricePerNight}`,
            image: r.images?.[0]?.imageUrl || defaultRooms[idx % defaultRooms.length].image,
          }));
          setRooms(mapped);
        }
      } catch {
        // Retain default rooms on error or when offline
      }
    }
    loadRoomTypes();
  }, []);
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
        {/* Soft elegant gradient overlay - clear top to keep colors, fading to white/dark canvas at bottom */}
        <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-transparent to-bg-dark dark:from-black/30 dark:via-black/25 dark:to-[#070c17] transition-all duration-300" />
        
        <div className="relative z-10 text-center px-6">
          <span className="text-primary text-xs font-semibold tracking-[0.25em] uppercase mb-3 block">
            Accommodation Directory
          </span>
          <h1 className="font-serif font-bold text-4xl sm:text-5xl text-[#1B2A4A] dark:text-white tracking-wide">
            Luxury Rooms & Suites
          </h1>
        </div>
      </section>

      {/* Ongoing Offers Section */}
      <section className="py-20 bg-section-alt border-b border-card-border transition-colors duration-300">
        <div className="max-w-7xl mx-auto px-6">
          <div className="text-center max-w-xl mx-auto mb-16">
            <span className="text-primary text-xs font-semibold tracking-[0.2em] uppercase block mb-3">
              Hot Packages
            </span>
            <h2 className="font-serif font-bold text-3xl text-text-primary">
              Ongoing Offers
            </h2>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            {offers.map((offer, idx) => (
              <Card key={idx} className="flex flex-col overflow-hidden hover:border-primary/25 hover:shadow-primary/5 group">
                <div className="relative h-52 overflow-hidden">
                  <img
                    src={offer.image}
                    alt={offer.title}
                    className="object-cover w-full h-full transition-transform duration-700 group-hover:scale-105"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/50 to-transparent" />
                </div>
                <div className="p-6 flex flex-col flex-1">
                  <h3 className="font-serif font-semibold text-base text-text-primary mb-4 line-clamp-2">
                    {offer.title}
                  </h3>
                  <ul className="space-y-2 mb-6 flex-1">
                    {offer.features.map((feat, fIdx) => (
                      <li key={fIdx} className="text-xs text-text-secondary flex items-center gap-2">
                        <span className="w-1 h-1 rounded-full bg-primary shrink-0" />
                        {feat}
                      </li>
                    ))}
                  </ul>
                  <Link href="/login" className="w-full text-center py-2.5 rounded-lg border border-primary/20 hover:border-primary text-primary hover:bg-primary/5 text-xs font-semibold uppercase tracking-wider transition-colors">
                    Book Now
                  </Link>
                </div>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Choose a Better Room Section */}
      <section className="py-20 bg-bg-dark transition-colors duration-300">
        <div className="max-w-7xl mx-auto px-6">
          <div className="text-center max-w-xl mx-auto mb-16">
            <span className="text-primary text-xs font-semibold tracking-[0.2em] uppercase block mb-3">
              Room Showroom
            </span>
            <h2 className="font-serif font-bold text-3xl text-text-primary">
              Choose a Better Room
            </h2>
          </div>

          <div className="grid gap-8 md:grid-cols-2 lg:grid-cols-4">
            {rooms.map((room, idx) => (
              <Card key={idx} className="flex flex-col overflow-hidden hover:border-primary/25 hover:shadow-primary/5 group">
                <div className="relative h-56 overflow-hidden">
                  <img
                    src={room.image}
                    alt={room.name}
                    className="object-cover w-full h-full transition-transform duration-700 group-hover:scale-105"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
                  
                  {/* Rating Capsule Overlay */}
                  <div className="absolute top-4 left-4 bg-black/75 backdrop-blur-md text-amber-500 text-[11px] font-bold px-2 py-0.5 rounded flex items-center gap-1">
                    <Star className="size-3 fill-current text-amber-500" />
                    <span>4.9</span>
                  </div>
                </div>
                <div className="p-4 flex flex-col flex-1">
                  <h3 className="font-serif font-semibold text-base text-text-primary">
                    {room.name}
                  </h3>
                  
                  {/* Price & Reviews Hierarchy */}
                  <div className="flex justify-between items-end mt-4 mb-5">
                    <span className="text-text-primary text-[20px] font-medium font-sans">{room.price}<span className="text-xs text-text-secondary font-normal ml-1">/night</span></span>
                    <span className="text-[10px] text-text-secondary uppercase tracking-wider font-semibold">240+ reviews</span>
                  </div>

                  <Link href="/login" className="block w-full text-center py-2.5 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg text-xs font-semibold transition-colors mt-auto">
                    Book now
                  </Link>
                </div>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Reservation queries */}
      <section className="py-16 bg-gradient-to-r from-primary/10 via-bg-dark to-secondary/10 border-t border-card-border text-center px-6 transition-colors duration-300">
        <div className="max-w-2xl mx-auto flex flex-col items-center">
          <h2 className="font-serif font-bold text-2xl sm:text-3xl text-text-primary mb-3">
            For reservation or queries?
          </h2>
          <p className="text-text-secondary text-xs max-w-sm leading-relaxed mb-6">
            Get assistance with planning your itinerary or booking private corporate spaces.
          </p>
          <Link href="/contact">
            <button className="px-7 py-2.5 rounded-lg bg-primary hover:bg-primary/90 text-primary-foreground font-semibold text-xs transition-all duration-300 hover:-translate-y-0.5 shadow-lg shadow-primary/20 cursor-pointer">
              Contact us now
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

// Internal custom Card for this page
function Card({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn("bg-card-dark dark:bg-[#121926]/40 border border-card-border dark:border-white/5 rounded-xl shadow-lg dark:shadow-xl transition-all duration-300", className)}>
      {children}
    </div>
  );
}
